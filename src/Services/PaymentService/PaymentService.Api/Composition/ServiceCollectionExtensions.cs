using BuildingBlocks.Authentication;
using BuildingBlocks.Hosting;
using BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using PaymentService.Api.Data;
using PaymentService.Api.Messaging;

namespace PaymentService.Api.Composition;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddAutoHubLiveness()
            .AddDbContextCheck<PaymentDbContext>(tags: [HealthCheckTags.Ready])
            .AddAutoHubRabbitMqReady(configuration);
        services.AddDbContext<PaymentDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PaymentDb")));
        services.AddHostedService<PaymentOutboxPublisher>();
        services.AddHttpClient("sales", client =>
                client.BaseAddress = new Uri(configuration["ExternalServices:SalesApiBaseUrl"]!))
            .AddStandardResilienceHandler();
        services.AddHttpClient("rentals", client =>
                client.BaseAddress = new Uri(configuration["ExternalServices:RentalApiBaseUrl"]!))
            .AddStandardResilienceHandler();
        services.AddOpenTelemetryObservability(configuration, "payment-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
