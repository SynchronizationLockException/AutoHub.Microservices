using BuildingBlocks.Hosting;
using BuildingBlocks.Messaging.Consumers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Text;

namespace AutoHub.BuildingBlocks.Tests;

public sealed class RabbitMqRetryHelperTests
{
    [Fact]
    public void TryGetRetryCount_ReturnsZero_WhenHeaderMissing()
    {
        var count = RabbitMqRetryHelper.TryGetRetryCount(null);
        Assert.Equal(0, count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void TryGetRetryCount_ParsesByteArrayHeader(int expected)
    {
        var headers = new Dictionary<string, object>
        {
            ["x-retry"] = Encoding.UTF8.GetBytes(expected.ToString())
        };

        Assert.Equal(expected, RabbitMqRetryHelper.TryGetRetryCount(headers));
    }

    [Fact]
    public void TryGetRetryCount_ParsesIntHeader()
    {
        var headers = new Dictionary<string, object> { ["x-retry"] = 2 };
        Assert.Equal(2, RabbitMqRetryHelper.TryGetRetryCount(headers));
    }
}

public sealed class ProductionSecretValidationTests
{
    [Fact]
    public void ValidateProductionSecrets_AllowsStrongSecret()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalApi:Secret"] = "prod-secret-1234567890"
            })
            .Build();

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        ProductionSecretValidationExtensions.ValidateProductionSecrets(config, env);
    }

    [Fact]
    public void ValidateProductionSecrets_ThrowsOnDefaultSecret()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalApi:Secret"] = "dev-internal-secret"
            })
            .Build();

        var env = new TestHostEnvironment { EnvironmentName = Environments.Production };
        Assert.Throws<InvalidOperationException>(() =>
            ProductionSecretValidationExtensions.ValidateProductionSecrets(config, env));
    }
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
}
