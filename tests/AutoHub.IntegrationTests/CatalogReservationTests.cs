extern alias Catalog;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;

namespace AutoHub.IntegrationTests;

public sealed class CatalogReservationTests : IAsyncLifetime
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
                .WithDatabase("catalogreservationtest")
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
    public async Task ReserveSameCar_Concurrently_OnlyOneSucceeds()
    {
        if (_skipReason is not null)
        {
            Assert.Fail(_skipReason);
        }

        var cars = await _client!.GetFromJsonAsync<List<Catalog::CarCatalogService.Api.Models.Car>>("/api/cars");
        var carId = cars!.First().Id;

        var tasks = Enumerable.Range(0, 2).Select(async i =>
        {
            var response = await _client.PostAsJsonAsync(
                $"/api/cars/{carId}/reservations",
                new { purpose = "Rent", holderReference = $"holder-{i}", ttlMinutes = 15 });
            return response.StatusCode;
        });

        var results = await Task.WhenAll(tasks);
        Assert.Equal(1, results.Count(x => x == HttpStatusCode.Created));
        Assert.Equal(1, results.Count(x => x == HttpStatusCode.BadRequest));
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
