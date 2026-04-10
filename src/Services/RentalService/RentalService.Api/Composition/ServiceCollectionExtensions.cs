using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Http.Resilience;
using RentalService.Api.Data;
using RentalService.Api.Messaging;
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
        services.AddHostedService<RentalOutboxPublisher>();
        services.AddHttpClient("catalog", client =>
                client.BaseAddress = new Uri(configuration.GetRequiredValue("ExternalServices:CatalogApiBaseUrl")))
            .AddStandardResilienceHandler();
        services.AddOpenTelemetryObservability(configuration, "rental-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
