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
            options.UseNpgsql(configuration.GetConnectionString("SalesDb")));
        services.AddHostedService<SalesOutboxPublisher>();
        services.AddHttpClient("catalog", client =>
                client.BaseAddress = new Uri(configuration["ExternalServices:CatalogApiBaseUrl"]!))
            .AddStandardResilienceHandler();
        services.AddOpenTelemetryObservability(configuration, "sales-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
