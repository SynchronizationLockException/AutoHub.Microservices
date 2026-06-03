using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;

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
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("gateway", limiter =>
            {
                limiter.PermitLimit = 200;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
