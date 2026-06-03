using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Http.Resilience;
using SalesService.Api.Data;
using SalesService.Api.Messaging;
using SalesService.Api.Services;
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
        services.AddScoped<CatalogReservationClient>();
        services.AddScoped<SalesSagaService>();
        services.AddHostedService<SalesOutboxPublisher>();
        services.AddHostedService<SagaTimeoutWorker>();
        services.AddHttpClient("catalog", client =>
            {
                client.BaseAddress = new Uri(configuration.GetRequiredValue("ExternalServices:CatalogApiBaseUrl"));
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddStandardResilienceHandler();
        services.AddOpenTelemetryObservability(configuration, "sales-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
