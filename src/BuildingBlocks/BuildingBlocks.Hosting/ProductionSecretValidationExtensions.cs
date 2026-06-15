using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BuildingBlocks.Hosting;

public static class ProductionSecretValidationExtensions
{
    private static readonly HashSet<string> ForbiddenInternalSecrets = new(StringComparer.Ordinal)
    {
        "dev-internal-secret",
        "change-me-internal-secret",
        "change-me"
    };

    public static void ValidateProductionSecrets(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var internalSecret = configuration["InternalApi:Secret"];
        if (string.IsNullOrWhiteSpace(internalSecret) || ForbiddenInternalSecrets.Contains(internalSecret))
        {
            throw new InvalidOperationException(
                "InternalApi:Secret must be set to a strong unique value in Production.");
        }
    }
}
