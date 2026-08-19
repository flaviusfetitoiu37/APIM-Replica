using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ApimReplica.Data;
using System.Text.Json;
using ApimReplica.Dtos;
using ApimReplica.Models;
using ApimReplica.Services;
using Npgsql;

namespace ApimReplica.Controllers;

[ApiController]
[Route("apis")]
public class ApisController : ControllerBase
{
    private static readonly HashSet<string> OperationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "put", "post", "delete", "options", "head", "patch", "trace"
    };

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProxyConfigService _proxyConfig;
    private readonly AiService _aiService;


    public ApisController(AppDbContext db, IHttpClientFactory httpClientFactory, ProxyConfigService proxyConfig, AiService aiService)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _proxyConfig = proxyConfig;
        _aiService = aiService;
    }




    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var apis = await _db.Apis
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Type,
                a.BaseUrl,
                HealthStatus = a.HealthStatus ?? "unknown",
                a.LastLatencyMs,
                a.LastCheckedAt,
                a.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(apis);
    }




    [HttpPost("rest")]
    public async Task<IActionResult> CreateRest(CreateRestApiDto dto, CancellationToken ct)
    {
        var name = dto.Name.Trim();

        if (!IsValidRouteName(name))
            return BadRequest("Name must be 1-64 characters of letters, digits, '-' or '_'.");

        if (!IsAbsoluteHttpUrl(dto.BaseUrl))
            return BadRequest("baseUrl must be an absolute http(s) URL.");

        if (!IsAbsoluteHttpUrl(dto.SchemaUrl))
            return BadRequest("schemaUrl must be an absolute http(s) URL.");

        if (!string.IsNullOrWhiteSpace(dto.HealthCheckUrl) && !IsAbsoluteHttpUrl(dto.HealthCheckUrl))
            return BadRequest("healthCheckUrl must be an absolute http(s) URL.");

        var lowered = name.ToLower();
        if (await _db.Apis.AnyAsync(a => a.Name.ToLower() == lowered, ct))
            return Conflict($"An API named '{name}' already exists.");

        var (schemaJson, downloadError) = await DownloadSchemaAsync(dto.SchemaUrl, ct);
        if (downloadError is not null)
            return downloadError;

        var api = new Api
        {
            Name = name,
            Type = "rest",
            BaseUrl = dto.BaseUrl,
            SchemaUrl = dto.SchemaUrl,
            HealthCheckUrl = string.IsNullOrWhiteSpace(dto.HealthCheckUrl) ? null : dto.HealthCheckUrl,
            Schema = schemaJson,
            HealthStatus = "unknown"
        };

        // Api and its first version are inserted in a single SaveChanges, so an API can
        // never end up stored without version 1. EF fills ApiId from the navigation.
        _db.Apis.Add(api);
        _db.SchemaVersions.Add(new ApiSchemaVersion
        {
            Api = api,
            VersionNumber = 1,
            Content = schemaJson,
            ContentHash = ComputeHash(schemaJson)
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost a race against a concurrent registration of the same name.
            return Conflict($"An API named '{name}' already exists.");
        }

        await _proxyConfig.ReloadAsync(ct);

        return Created($"/apis/{api.Id}", new
        {
            api.Id,
            api.Name,
            api.Type,
            api.BaseUrl,
            api.SchemaUrl,
            api.HealthCheckUrl,
            api.HealthStatus,
            api.CreatedAt
        });
    }




    [HttpPost("{id}/refresh")]
    public async Task<IActionResult> Refresh(int id, CancellationToken ct)
    {
        var api = await _db.Apis.FindAsync([id], ct);
        if (api is null)
            return NotFound();

        if (string.IsNullOrEmpty(api.SchemaUrl))
            return BadRequest("API has no schema URL.");

        var (schemaJson, downloadError) = await DownloadSchemaAsync(api.SchemaUrl, ct);
        if (downloadError is not null)
            return downloadError;

        var hash = ComputeHash(schemaJson);

        // The unique index on (ApiId, VersionNumber) rejects a version number that a
        // concurrent refresh already took; re-read and retry instead of duplicating it.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var lastVersion = await _db.SchemaVersions
                .Where(v => v.ApiId == id)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(ct);

            if (lastVersion is not null && lastVersion.ContentHash == hash)
                return Ok(new { message = "No changes.", version = lastVersion.VersionNumber });

            var version = new ApiSchemaVersion
            {
                ApiId = id,
                VersionNumber = (lastVersion?.VersionNumber ?? 0) + 1,
                Content = schemaJson,
                ContentHash = hash
            };

            _db.SchemaVersions.Add(version);
            api.Schema = schemaJson;

            try
            {
                await _db.SaveChangesAsync(ct);
                return Ok(new { message = "New version saved.", version = version.VersionNumber });
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                _db.Entry(version).State = EntityState.Detached;
            }
        }

        return Conflict("Another refresh for this API is in progress, try again.");
    }




    [HttpGet("{id}/diff")]
    public async Task<IActionResult> Diff(int id, int? from, int? to, CancellationToken ct)
    {
        if (from is null || to is null)
            return BadRequest("Query parameters 'from' and 'to' are required.");

        if (!await _db.Apis.AnyAsync(a => a.Id == id, ct))
            return NotFound($"API {id} not found.");

        int fromVersion = from.Value, toVersion = to.Value;

        var v1 = await _db.SchemaVersions
            .FirstOrDefaultAsync(v => v.ApiId == id && v.VersionNumber == fromVersion, ct);
        var v2 = await _db.SchemaVersions
            .FirstOrDefaultAsync(v => v.ApiId == id && v.VersionNumber == toVersion, ct);

        if (v1 is null || v2 is null)
            return NotFound("One or both versions not found.");

        if (!TryExtractPaths(v1.Content, out var oldPaths) || !TryExtractPaths(v2.Content, out var newPaths))
            return BadRequest("Stored schema is not a valid OpenAPI document.");

        return Ok(new
        {
            from = fromVersion,
            to = toVersion,
            added = newPaths.Except(oldPaths).ToList(),
            removed = oldPaths.Except(newPaths).ToList(),
            unchanged = oldPaths.Intersect(newPaths).Count()
        });
    }

    private static bool TryExtractPaths(string schemaJson, out HashSet<string> result)
    {
        result = new HashSet<string>();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(schemaJson);
        }
        catch (JsonException)
        {
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("paths", out var paths) ||
                paths.ValueKind != JsonValueKind.Object)
                return true;

            foreach (var path in paths.EnumerateObject())
            {
                // A path item may also hold "summary", "description", "parameters",
                // "servers" or "$ref" — only real operations count as endpoints.
                if (path.Value.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var method in path.Value.EnumerateObject())
                {
                    if (!OperationNames.Contains(method.Name))
                        continue;

                    result.Add($"{method.Name.ToUpperInvariant()} {path.Name}");
                }
            }
        }

        return true;
    }

    private static string ComputeHash(string content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static bool IsAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsValidRouteName(string name) =>
        name.Length is > 0 and <= 64 &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private async Task<(string Schema, IActionResult? Error)> DownloadSchemaAsync(string url, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        string body;
        try
        {
            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return (string.Empty, StatusCode(StatusCodes.Status502BadGateway,
                    $"Schema URL returned HTTP {(int)response.StatusCode}."));

            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (string.Empty, StatusCode(StatusCodes.Status504GatewayTimeout,
                "Schema URL timed out after 30s."));
        }
        catch (HttpRequestException ex)
        {
            return (string.Empty, StatusCode(StatusCodes.Status502BadGateway,
                $"Schema URL unreachable: {ex.Message}"));
        }

        // Schema and Content are jsonb columns; anything else fails at SaveChanges.
        if (!IsJson(body))
            return (string.Empty, BadRequest(
                "Schema is not valid JSON. Only JSON OpenAPI documents are supported, not YAML."));

        return (body, null);
    }

    private static bool IsJson(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }




    [HttpPost("{id}/ask")]
    public async Task<IActionResult> Ask(int id, AskDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Question))
            return BadRequest("Question is required.");

        var api = await _db.Apis.FindAsync([id], ct);
        if (api is null || string.IsNullOrEmpty(api.Schema))
            return NotFound("API or schema not found.");

        try
        {
            var answer = await _aiService.AskAsync(api.Schema, dto.Question, ct);
            return Ok(new { question = dto.Question, answer });
        }
        catch (AiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ex.Message);
        }
    }




    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var api = await _db.Apis.FindAsync([id], ct);
        if (api is null)
            return NotFound();

        // Same shape as the entity, but HealthStatus is coalesced exactly like in
        // GetAll so the field never differs between the list and the detail view.
        return Ok(new
        {
            api.Id,
            api.Name,
            api.Type,
            api.BaseUrl,
            api.SchemaUrl,
            api.HealthCheckUrl,
            api.Schema,
            HealthStatus = api.HealthStatus ?? "unknown",
            api.LastLatencyMs,
            api.LastCheckedAt,
            api.CreatedAt
        });
    }




    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var api = await _db.Apis.FindAsync([id], ct);
        if (api is null)
            return NotFound();

        _db.Apis.Remove(api);
        await _db.SaveChangesAsync(ct);
        await _proxyConfig.ReloadAsync(ct);

        return NoContent();
    }




    [HttpGet("{id}/versions")]
    public async Task<IActionResult> GetVersions(int id, CancellationToken ct)
    {
        if (!await _db.Apis.AnyAsync(a => a.Id == id, ct))
            return NotFound($"API {id} not found.");

        // Sizes are measured in SQL so the schema blobs never leave the database.
        // EF cannot translate string.Length here: Content is jsonb and Postgres has
        // no length(jsonb) overload, hence the explicit ::text cast.
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT "VersionNumber", "FetchedAt", length("Content"::text)
            FROM "SchemaVersions"
            WHERE "ApiId" = @apiId
            ORDER BY "VersionNumber" DESC
            """;

        var apiIdParameter = command.CreateParameter();
        apiIdParameter.ParameterName = "apiId";
        apiIdParameter.Value = id;
        command.Parameters.Add(apiIdParameter);

        var result = new List<object>();

        await _db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new
                {
                    VersionNumber = reader.GetInt32(0),
                    FetchedAt = reader.GetDateTime(1),
                    SizeBytes = reader.GetInt32(2)
                });
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }

        return Ok(result);
    }
}
