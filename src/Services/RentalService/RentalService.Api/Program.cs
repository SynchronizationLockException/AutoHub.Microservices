using BuildingBlocks.Hosting;
using BuildingBlocks.Hosting.Persistence;
using RentalService.Api.Composition;
using RentalService.Api.Data;
using RentalService.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutoHubSerilog();

builder.Services.AddRentalService(builder.Configuration);
ProductionSecretValidationExtensions.ValidateProductionSecrets(builder.Configuration, builder.Environment);

var app = builder.Build();

if (args.IsMigrationMode())
{
    await app.Services.InitializeRentalDataAsync(runMigrations: true);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.Services.InitializeRentalDataAsync(runMigrations: false);

app.UseAutoHubProblemDetails();
app.UseAutoHubCorrelationId();
app.MapAutoHubHealthEndpoints();
app.UseAuthentication();
app.UseAuthorization();

app.MapRentalEndpoints();
app.MapSagaInternalEndpoints();

app.Run();
