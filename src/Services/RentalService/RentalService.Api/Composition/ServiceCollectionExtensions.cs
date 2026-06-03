using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Http.Resilience;
using RentalService.Api.Data;
using RentalService.Api.Messaging;
using RentalService.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace RentalService.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRentalService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddAutoHubLiveness()
            .AddDbContextCheck<RentalDbContext>(tags: [HealthCheckTags.Ready])
            .AddAutoHubRabbitMqReady(configuration);
        services.AddDbContext<RentalDbContext>(options =>
            options.UseNpgsql(configuration.GetRequiredConnectionString("RentalDb")));
        services.AddScoped<CatalogReservationClient>();
        services.AddScoped<RentalSagaService>();
        services.AddHostedService<RentalOutboxPublisher>();
        services.AddHostedService<SagaTimeoutWorker>();
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
        services.AddOpenTelemetryObservability(configuration, "rental-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
