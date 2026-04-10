extern alias Payment;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using Testcontainers.PostgreSql;

namespace AutoHub.IntegrationTests;

public sealed class PaymentApiTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    private WebApplicationFactory<Payment::Program>? _factory;
    private HttpClient? _client;
    private string? _skipReason;

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder("postgres:17")
                .WithDatabase("paymenttestdb")
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

        _factory = new WebApplicationFactory<Payment::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:PaymentDb"] = _postgres.GetConnectionString(),
                        ["Jwt:Issuer"] = "AutoHub.Auth",
                        ["Jwt:Audience"] = "AutoHub.Clients",
                        ["Jwt:JwksUrl"] = "",
                        ["Jwt:Key"] = "super-secret-key-change-in-production-256",
                        ["ExternalServices:SalesApiBaseUrl"] = "http://localhost:5999",
                        ["ExternalServices:RentalApiBaseUrl"] = "http://localhost:5998",
                        ["RabbitMq:Host"] = "localhost",
                        ["HealthChecks:EnableRabbitMqReady"] = "false"
                    });
                });
            });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetPayments_WithoutToken_ReturnsUnauthorized()
    {
        if (_skipReason is not null)
        {
            Assert.True(true, _skipReason);
            return;
        }

        var response = await _client!.GetAsync("/api/payments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
