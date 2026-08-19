using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ApimReplica.Data;

namespace ApimReplica.Services;

public class HealthCheckService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly EmailService _email;

    public HealthCheckService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<HealthCheckService> logger, EmailService email)

    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _email = email;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CheckAllAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        // Only the columns the check needs — the schema blobs stay in the database.
        var apis = await db.Apis
            .Select(a => new { a.Id, a.Name, a.BaseUrl, a.HealthCheckUrl, a.HealthStatus })
            .ToListAsync(ct);

        foreach (var api in apis)
        {
            string status;
            int? latency;

            var usingFallbackUrl = string.IsNullOrEmpty(api.HealthCheckUrl);
            var url = usingFallbackUrl ? api.BaseUrl : api.HealthCheckUrl!;

            var sw = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(url, ct);
                sw.Stop();

                // Without a dedicated health URL we probe BaseUrl, which commonly answers
                // 404 while the API itself is fine — only a 5xx means the server is sick.
                var ok = usingFallbackUrl
                    ? (int)response.StatusCode < 500
                    : response.IsSuccessStatusCode;

                status = ok ? "healthy" : "unhealthy";
                latency = (int)sw.ElapsedMilliseconds;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                status = "down";
                latency = null;
                _logger.LogWarning("Health check failed for {Name}: {Message}", api.Name, ex.Message);
            }

            var checkedAt = DateTime.UtcNow;

            // Written per API, so one failing check no longer discards the whole cycle.
            await db.Apis
                .Where(a => a.Id == api.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.HealthStatus, status)
                    .SetProperty(a => a.LastLatencyMs, latency)
                    .SetProperty(a => a.LastCheckedAt, checkedAt), ct);

            if (api.HealthStatus != "down" && status == "down")
            {
                try
                {
                    await _email.SendAsync(
                        $"[APIM] {api.Name} is DOWN",
                        $"API '{api.Name}' stopped responding.\nURL: {url}\nTime: {checkedAt:u}",
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send down alert for {Name}.", api.Name);
                }
            }
        }
    }
}
