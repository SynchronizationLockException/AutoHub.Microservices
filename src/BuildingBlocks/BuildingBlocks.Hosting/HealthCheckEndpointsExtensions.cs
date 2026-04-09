using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace BuildingBlocks.Hosting;

public static class HealthCheckEndpointsExtensions
{
    public static WebApplication MapAutoHubHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(HealthCheckTags.Live)
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(HealthCheckTags.Ready)
        });

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains(HealthCheckTags.Ready)
        });

        return app;
    }
}
