using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Enrichers.Span;

namespace BuildingBlocks.Hosting;

public static class SerilogExtensions
{
    public static IHostBuilder UseAutoHubSerilog(this IHostBuilder hostBuilder) =>
        hostBuilder.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithSpan()
                .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
                .WriteTo.Console();
        });
}
