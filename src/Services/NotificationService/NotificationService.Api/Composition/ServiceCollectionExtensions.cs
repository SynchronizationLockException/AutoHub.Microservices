using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
using NotificationService.Api.Messaging;
using NotificationService.Api.Services;

namespace NotificationService.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddAutoHubProblemDetails();
        services.AddHealthChecks()
            .AddAutoHubLiveness()
            .AddDbContextCheck<NotificationDbContext>(tags: [HealthCheckTags.Ready])
            .AddAutoHubRabbitMqReady(configuration);
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(configuration.GetRequiredConnectionString("NotificationDb")));
        services.AddSingleton<LogNotificationSender>();
        services.AddSingleton<EmailNotificationSender>();
        services.AddSingleton<INotificationSender>(sp => new CompositeNotificationSender(
            [sp.GetRequiredService<EmailNotificationSender>(), sp.GetRequiredService<LogNotificationSender>()]));
        services.AddHostedService<NotificationEventsConsumer>();
        services.AddOpenTelemetryObservability(configuration, "notification-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
