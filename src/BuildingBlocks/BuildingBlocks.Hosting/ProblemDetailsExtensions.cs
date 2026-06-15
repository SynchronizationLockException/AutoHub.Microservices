using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Hosting;

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddAutoHubProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    public static IApplicationBuilder UseAutoHubProblemDetails(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
