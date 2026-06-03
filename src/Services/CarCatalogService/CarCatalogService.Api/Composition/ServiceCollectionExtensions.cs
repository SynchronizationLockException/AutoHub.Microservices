using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using CarCatalogService.Api.Data;
using CarCatalogService.Api.Messaging;
using CarCatalogService.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CarCatalogService.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarCatalogService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddAutoHubLiveness()
            .AddDbContextCheck<CarCatalogDbContext>(tags: [HealthCheckTags.Ready])
            .AddAutoHubRabbitMqReady(configuration);
        services.AddDbContext<CarCatalogDbContext>(options =>
            options.UseNpgsql(configuration.GetRequiredConnectionString("CatalogDb")));
        services.AddScoped<ReservationService>();
        services.AddScoped<SagaCompletionNotifier>();
        services.AddHttpClient("rental-internal");
        services.AddHttpClient("sales-internal");
        services.AddHostedService<CatalogEventsConsumer>();
        services.AddHostedService<CatalogDeadLetterWorker>();
        services.AddHostedService<ReservationExpiryWorker>();
        services.AddOpenTelemetryObservability(configuration, "car-catalog-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
