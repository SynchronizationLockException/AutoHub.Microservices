using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiGateway.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddCheck(
                "gateway",
                () => HealthCheckResult.Healthy(),
                tags: [HealthCheckTags.Live, HealthCheckTags.Ready]);
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();
        services
            .AddReverseProxy()
            .LoadFromConfig(configuration.GetSection("ReverseProxy"));
        services.AddOpenTelemetryObservability(configuration, "api-gateway");

        return services;
    }
}
