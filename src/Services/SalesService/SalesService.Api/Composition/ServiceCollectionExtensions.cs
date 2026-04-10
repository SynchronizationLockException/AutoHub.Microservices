using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Http.Resilience;
using SalesService.Api.Data;
using SalesService.Api.Messaging;
using Microsoft.EntityFrameworkCore;

namespace SalesService.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSalesService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddAutoHubLiveness()
            .AddDbContextCheck<SalesDbContext>(tags: [HealthCheckTags.Ready])
            .AddAutoHubRabbitMqReady(configuration);
        services.AddDbContext<SalesDbContext>(options =>
            options.UseNpgsql(configuration.GetRequiredConnectionString("SalesDb")));
        services.AddHostedService<SalesOutboxPublisher>();
        services.AddHttpClient("catalog", client =>
            {
                client.BaseAddress = new Uri(configuration.GetRequiredValue("ExternalServices:CatalogApiBaseUrl"));
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
                options.Retry.MaxRetryAttempts = 2;
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 10;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            });
        services.AddOpenTelemetryObservability(configuration, "sales-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
