using BuildingBlocks.Hosting;
using BuildingBlocks.Hosting.Persistence;
using CarCatalogService.Api.Composition;
using CarCatalogService.Api.Data;
using CarCatalogService.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCarCatalogService(builder.Configuration);

var app = builder.Build();

if (args.IsMigrationMode())
{
    await app.Services.InitializeCatalogDataAsync(runMigrations: true);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.Services.InitializeCatalogDataAsync(runMigrations: app.Environment.IsDevelopment());

app.MapAutoHubHealthEndpoints();
app.UseAuthentication();
app.UseAuthorization();

app.MapCatalogEndpoints();

app.Run();

public partial class Program;
