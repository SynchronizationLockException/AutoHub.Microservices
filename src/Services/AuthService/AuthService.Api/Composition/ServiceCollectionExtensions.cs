using AuthService.Api.Data;
using AuthService.Api.Security;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

namespace AuthService.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddDbContext<AuthDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("AuthDb")));
        services.AddSingleton<RsaJwtSigningKeys>();
        services.AddSingleton<TokenService>();
        services.AddOpenTelemetryObservability(configuration, "auth-service");

        services.AddHealthChecks()
            .AddAutoHubLiveness()
            .AddDbContextCheck<AuthDbContext>(tags: [HealthCheckTags.Ready]);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", context =>
            {
                var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }
}
