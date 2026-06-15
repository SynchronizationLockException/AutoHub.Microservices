using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using CarCatalogService.Api.Data;
using CarCatalogService.Api.Messaging;
using CarCatalogService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;

namespace CarCatalogService.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarCatalogService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddMemoryCache();
        services.AddAutoHubProblemDetails();
        services.AddHealthChecks()
            .AddAutoHubLiveness()
            .AddDbContextCheck<CarCatalogDbContext>(tags: [HealthCheckTags.Ready])
            .AddAutoHubRabbitMqReady(configuration);
        services.AddDbContext<CarCatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetRequiredConnectionString("CatalogDb")));
        services.AddScoped<ReservationService>();
        services.AddScoped<SagaCompletionNotifier>();
        services.AddHttpClient("rental-internal")
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
                options.Retry.MaxRetryAttempts = 2;
            });
        services.AddHttpClient("sales-internal")
            .AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
                options.Retry.MaxRetryAttempts = 2;
            });
        services.AddHostedService<CatalogEventsConsumer>();
        services.AddHostedService<CatalogDeadLetterWorker>();
        services.AddHostedService<ReservationExpiryWorker>();
        services.AddOpenTelemetryObservability(configuration, "car-catalog-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
