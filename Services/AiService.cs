using System.Text;
using System.Text.Json;

namespace ApimReplica.Services;

public class AiUnavailableException : Exception
{
    public AiUnavailableException(string message) : base(message)
    {
    }
}

public class AiService
{
    private static readonly HashSet<string> OperationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "put", "post", "delete", "options", "head", "patch", "trace"
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public AiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> AskAsync(string schemaJson, string question, CancellationToken ct = default)
    {
        var summary = SummarizeSchema(schemaJson);

        var prompt = $"""
            You are an assistant that answers questions about a REST API.
            Use only the endpoint list below. If the answer is not there, say so.

            Endpoints:
            {summary}

            Question: {question}
            """;

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);

        var body = new
        {
            model = "llama3.2",
            prompt,
            stream = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var response = await client.PostAsync("http://localhost:11434/api/generate", content, ct);

            if (!response.IsSuccessStatusCode)
                throw new AiUnavailableException(
                    $"AI assistant returned HTTP {(int)response.StatusCode}. Is Ollama running with the llama3.2 model?");

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("response", out var answer)
                ? answer.GetString() ?? ""
                : "";
        }
        catch (HttpRequestException ex)
        {
            throw new AiUnavailableException(
                $"AI assistant unreachable at localhost:11434 ({ex.Message}). Start it with 'ollama serve'.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new AiUnavailableException("AI assistant timed out after 2 minutes.");
        }
        catch (JsonException ex)
        {
            throw new AiUnavailableException($"AI assistant returned a malformed response: {ex.Message}");
        }
    }

    private static string SummarizeSchema(string schemaJson)
    {
        var sb = new StringBuilder();

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(schemaJson);
        }
        catch (JsonException)
        {
            return "(no endpoints found)";
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("paths", out var paths) ||
                paths.ValueKind != JsonValueKind.Object)
                return "(no endpoints found)";

            foreach (var path in paths.EnumerateObject())
            {
                // Skip "summary", "parameters", "$ref" and friends — only operations.
                if (path.Value.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var method in path.Value.EnumerateObject())
                {
                    if (!OperationNames.Contains(method.Name))
                        continue;

                    var desc = method.Value.ValueKind == JsonValueKind.Object &&
                               method.Value.TryGetProperty("summary", out var s)
                        ? s.GetString()
                        : "";

                    sb.AppendLine($"{method.Name.ToUpperInvariant()} {path.Name} — {desc}");
                }
            }
        }

        return sb.Length == 0 ? "(no endpoints found)" : sb.ToString();
    }
}
