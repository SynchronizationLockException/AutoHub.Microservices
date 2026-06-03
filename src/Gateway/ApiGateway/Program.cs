using ApiGateway.Composition;
using BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutoHubSerilog();

builder.Services.AddApiGateway(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAutoHubCorrelationId();
app.MapAutoHubHealthEndpoints();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy().RequireRateLimiting("gateway");

app.Run();
