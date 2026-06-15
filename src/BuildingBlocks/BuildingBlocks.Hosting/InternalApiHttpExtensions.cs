using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Hosting;

public static class InternalApiHttpExtensions
{
    public static void ApplyInternalSecret(this IConfiguration configuration, HttpRequestMessage request)
    {
        var secret = configuration["InternalApi:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        request.Headers.Remove(InternalApiExtensions.SecretHeaderName);
        request.Headers.Add(InternalApiExtensions.SecretHeaderName, secret);
    }
}
