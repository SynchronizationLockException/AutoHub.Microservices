using BuildingBlocks.Hosting;
using BuildingBlocks.Hosting.Persistence;
using SalesService.Api.Composition;
using SalesService.Api.Data;
using SalesService.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSalesService(builder.Configuration);

var app = builder.Build();

if (args.IsMigrationMode())
{
    await app.Services.InitializeSalesDataAsync(runMigrations: true);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.Services.InitializeSalesDataAsync(runMigrations: false);

app.MapAutoHubHealthEndpoints();
app.UseAuthentication();
app.UseAuthorization();

app.MapSalesEndpoints();

app.Run();
