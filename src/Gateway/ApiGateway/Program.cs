using ApiGateway.Composition;
using BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutoHubSerilog();

builder.Services.AddApiGateway(builder.Configuration);
ProductionSecretValidationExtensions.ValidateProductionSecrets(builder.Configuration, builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAutoHubProblemDetails();
app.UseAutoHubCorrelationId();
app.MapAutoHubHealthEndpoints();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy().RequireRateLimiting("gateway");

app.Run();
