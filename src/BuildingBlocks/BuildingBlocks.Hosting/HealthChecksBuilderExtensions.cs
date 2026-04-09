using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Sockets;

namespace BuildingBlocks.Hosting;

public static class HealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddAutoHubLiveness(this IHealthChecksBuilder builder) =>
        builder.AddCheck(
            "live",
            () => HealthCheckResult.Healthy(),
            tags: [HealthCheckTags.Live]);

    public static IHealthChecksBuilder AddAutoHubRabbitMqReady(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
    {
        if (!configuration.GetValue("HealthChecks:EnableRabbitMqReady", true))
        {
            return builder;
        }

        var host = configuration["RabbitMq:Host"] ?? "localhost";

        return builder.AddAsyncCheck(
            "rabbitmq",
            async () =>
            {
                try
                {
                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(host, 5672);
                    return HealthCheckResult.Healthy();
                }
                catch (Exception ex)
                {
                    return HealthCheckResult.Unhealthy("Cannot reach RabbitMQ AMQP port.", ex);
                }
            },
            tags: [HealthCheckTags.Ready]);
    }
}
