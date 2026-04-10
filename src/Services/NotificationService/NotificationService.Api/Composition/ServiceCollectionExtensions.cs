using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
using NotificationService.Api.Messaging;

namespace NotificationService.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddAutoHubLiveness()
            .AddDbContextCheck<NotificationDbContext>(tags: [HealthCheckTags.Ready])
            .AddAutoHubRabbitMqReady(configuration);
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(configuration.GetRequiredConnectionString("NotificationDb")));
        services.AddHostedService<NotificationEventsConsumer>();
        services.AddOpenTelemetryObservability(configuration, "notification-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
