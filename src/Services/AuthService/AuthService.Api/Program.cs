using AuthService.Api.Composition;
using AuthService.Api.Data;
using AuthService.Api.Endpoints;
using AuthService.Api.Security;
using BuildingBlocks.Hosting;
using BuildingBlocks.Hosting.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthService(builder.Configuration);

var app = builder.Build();

if (args.IsMigrationMode())
{
    await app.Services.InitializeAuthDataAsync(runMigrations: true);
    return;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.Services.InitializeAuthDataAsync(runMigrations: false);

app.MapAutoHubHealthEndpoints();
app.MapGet("/.well-known/jwks.json", (RsaJwtSigningKeys keys) =>
    Results.Content(keys.GetJwksJson(), "application/json")).AllowAnonymous();
app.UseRateLimiter();
app.MapAuthEndpoints();

app.Run();
