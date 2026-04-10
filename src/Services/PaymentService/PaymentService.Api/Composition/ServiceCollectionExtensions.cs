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
            options.UseNpgsql(configuration.GetRequiredConnectionString("PaymentDb")));
        services.AddHostedService<PaymentOutboxPublisher>();
        services.AddHttpClient("sales", client =>
            {
                client.BaseAddress = new Uri(configuration.GetRequiredValue("ExternalServices:SalesApiBaseUrl"));
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
        services.AddHttpClient("rentals", client =>
            {
                client.BaseAddress = new Uri(configuration.GetRequiredValue("ExternalServices:RentalApiBaseUrl"));
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
        services.AddOpenTelemetryObservability(configuration, "payment-service");
        services.AddAutoHubJwtBearer(configuration);
        services.AddAuthorization();

        return services;
    }
}
