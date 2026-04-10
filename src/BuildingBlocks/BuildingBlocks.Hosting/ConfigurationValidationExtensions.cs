using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Hosting;

public static class ConfigurationValidationExtensions
{
    public static string GetRequiredValue(this IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{key}' is required.");
        }

        return value;
    }

    public static string GetRequiredConnectionString(this IConfiguration configuration, string name)
    {
        var value = configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Connection string '{name}' is required.");
        }

        return value;
    }
}
