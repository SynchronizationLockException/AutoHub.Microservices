using ApiGateway.Composition;
using BuildingBlocks.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiGateway(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAutoHubHealthEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();
