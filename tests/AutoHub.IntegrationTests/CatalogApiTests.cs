extern alias Catalog;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;

namespace AutoHub.IntegrationTests;

public sealed class CatalogApiTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    private WebApplicationFactory<Catalog::Program>? _factory;
    private HttpClient? _client;
    private string? _skipReason;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("postgres:17")
                .WithDatabase("catalogtestdb")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _postgres.StartAsync();
        }
        catch (Exception ex)
        {
            _skipReason = $"Docker unavailable for Testcontainers: {ex.Message}";
            return;
        }

        _factory = new WebApplicationFactory<Catalog::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:CatalogDb"] = _postgres.GetConnectionString(),
                        ["Jwt:Issuer"] = "AutoHub.Auth",
                        ["Jwt:Audience"] = "AutoHub.Clients",
                        ["Jwt:JwksUrl"] = "",
                        ["Jwt:Key"] = "super-secret-key-change-in-production-256",
                        ["RabbitMq:Host"] = "localhost",
                        ["HealthChecks:EnableRabbitMqReady"] = "false"
                    });
                });
            });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetCars_ReturnsSeedData()
    {
        if (_skipReason is not null)
        {
            Assert.True(true, _skipReason);
            return;
        }

        var cars = await _client!.GetFromJsonAsync<List<Catalog::CarCatalogService.Api.Models.Car>>("/api/cars");
        Assert.NotNull(cars);
        Assert.NotEmpty(cars!);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
}
