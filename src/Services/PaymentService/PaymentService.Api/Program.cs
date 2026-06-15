using BuildingBlocks.Hosting;
using BuildingBlocks.Hosting.Persistence;
using PaymentService.Api.Composition;
using PaymentService.Api.Data;
using PaymentService.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseAutoHubSerilog();

builder.Services.AddPaymentService(builder.Configuration);
ProductionSecretValidationExtensions.ValidateProductionSecrets(builder.Configuration, builder.Environment);

var app = builder.Build();

if (args.IsMigrationMode())
{
    await app.Services.InitializePaymentDataAsync(runMigrations: true);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.Services.InitializePaymentDataAsync(runMigrations: app.Environment.IsDevelopment());

app.UseAutoHubProblemDetails();
app.UseAutoHubCorrelationId();
app.MapAutoHubHealthEndpoints();
app.UseAuthentication();
app.UseAuthorization();

app.MapPaymentEndpoints();

app.Run();

public partial class Program;
