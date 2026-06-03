using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Hosting;

public static class InternalApiExtensions
{
    public const string SecretHeaderName = "X-Internal-Secret";

    public static bool IsValidInternalRequest(this HttpContext context, IConfiguration configuration)
    {
        var expected = configuration["InternalApi:Secret"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return context.Request.Headers.TryGetValue(SecretHeaderName, out var provided) &&
               string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }
}
