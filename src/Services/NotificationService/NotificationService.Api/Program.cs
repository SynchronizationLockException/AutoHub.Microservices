using BuildingBlocks.Hosting;
using BuildingBlocks.Hosting.Persistence;
using NotificationService.Api.Composition;
using NotificationService.Api.Data;
using NotificationService.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutoHubSerilog();

builder.Services.AddNotificationService(builder.Configuration);
ProductionSecretValidationExtensions.ValidateProductionSecrets(builder.Configuration, builder.Environment);

var app = builder.Build();

if (args.IsMigrationMode())
{
    await app.Services.InitializeNotificationDataAsync(runMigrations: true);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.Services.InitializeNotificationDataAsync(runMigrations: app.Environment.IsDevelopment());

app.UseAutoHubProblemDetails();
app.UseAutoHubCorrelationId();
app.MapAutoHubHealthEndpoints();
app.UseAuthentication();
app.UseAuthorization();

app.MapNotificationEndpoints();

app.Run();

public partial class Program;
