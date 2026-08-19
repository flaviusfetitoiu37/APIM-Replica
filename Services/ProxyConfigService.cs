using Microsoft.EntityFrameworkCore;
using Yarp.ReverseProxy.Configuration;
using ApimReplica.Data;

namespace ApimReplica.Services;

public class ProxyConfigService
{
    private readonly InMemoryConfigProvider _provider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProxyConfigService> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    public ProxyConfigService(InMemoryConfigProvider provider, IServiceScopeFactory scopeFactory, ILogger<ProxyConfigService> logger)
    {
        _provider = provider;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        // Concurrent create/delete would otherwise race to publish the route table.
        await _reloadLock.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Only the three columns the route table needs — not the schema blobs.
            var apis = await db.Apis
                .OrderBy(a => a.Id)
                .Select(a => new { a.Id, a.Name, a.BaseUrl })
                .ToListAsync(ct);

            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var api in apis)
            {
                var key = api.Name.ToLowerInvariant();

                // Two routes matching the same path make YARP throw AmbiguousMatchException
                // on every request to it. The unique index prevents this; keep the guard
                // so pre-existing duplicates degrade to one working route instead of none.
                if (!seen.Add(key))
                {
                    _logger.LogWarning(
                        "Duplicate API name '{Name}' (id {Id}) — no route created, /proxy/{Key} already taken.",
                        api.Name, api.Id, key);
                    continue;
                }

                if (!Uri.TryCreate(api.BaseUrl, UriKind.Absolute, out _))
                {
                    _logger.LogWarning(
                        "API '{Name}' (id {Id}) has an invalid BaseUrl '{BaseUrl}' — no route created.",
                        api.Name, api.Id, api.BaseUrl);
                    continue;
                }

                routes.Add(new RouteConfig
                {
                    RouteId = $"route-{api.Id}",
                    ClusterId = $"cluster-{api.Id}",
                    Match = new RouteMatch { Path = $"/proxy/{key}/{{**catchall}}" },
                    Transforms = new[]
                    {
                        new Dictionary<string, string> { ["PathRemovePrefix"] = $"/proxy/{key}" }
                    }
                });

                clusters.Add(new ClusterConfig
                {
                    ClusterId = $"cluster-{api.Id}",
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        ["default"] = new DestinationConfig { Address = api.BaseUrl }
                    }
                });
            }

            _provider.Update(routes, clusters);
            _logger.LogInformation("Proxy config reloaded: {Count} route(s).", routes.Count);
        }
        finally
        {
            _reloadLock.Release();
        }
    }
}
