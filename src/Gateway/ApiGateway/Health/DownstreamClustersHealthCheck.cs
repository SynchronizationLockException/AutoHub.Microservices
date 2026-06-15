using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiGateway.Health;

public sealed class DownstreamClustersHealthCheck(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DownstreamClustersHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var clusters = configuration.GetSection("ReverseProxy:Clusters").GetChildren();
        var failures = new List<string>();

        foreach (var cluster in clusters)
        {
            var address = cluster.GetSection("Destinations").GetChildren()
                .Select(x => x["Address"])
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            var uri = new Uri(new Uri(address), "/health/ready");
            try
            {
                var client = httpClientFactory.CreateClient("downstream-health");
                using var response = await client.GetAsync(uri, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    failures.Add($"{cluster.Key}: HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Downstream health probe failed for {Cluster}", cluster.Key);
                failures.Add($"{cluster.Key}: {ex.Message}");
            }
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy("All downstream clusters are reachable.")
            : HealthCheckResult.Unhealthy(string.Join("; ", failures));
    }
}
