using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Authentication;

public sealed class JwksSigningKeyCache(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<JwksSigningKeyCache> logger)
{
    private readonly string _jwksUrl = configuration["Jwt:JwksUrl"]
        ?? throw new InvalidOperationException("Jwt:JwksUrl is required when using JWKS validation.");

    private readonly object _lock = new();
    private JsonWebKeySet? _jwks;
    private DateTimeOffset _cacheExpiresUtc = DateTimeOffset.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public IEnumerable<SecurityKey> ResolveSigningKeys(string? kid)
    {
        EnsureFreshKeys();
        var keys = _jwks?.GetSigningKeys() ?? Enumerable.Empty<SecurityKey>();
        if (string.IsNullOrEmpty(kid))
        {
            return keys;
        }

        return keys.Where(k => string.Equals(k.KeyId, kid, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureFreshKeys()
    {
        lock (_lock)
        {
            if (_jwks is not null && DateTimeOffset.UtcNow < _cacheExpiresUtc)
            {
                return;
            }

            RefreshLocked();
        }
    }

    public void Invalidate()
    {
        lock (_lock)
        {
            _jwks = null;
            _cacheExpiresUtc = DateTimeOffset.MinValue;
        }
    }

    private void RefreshLocked()
    {
        try
        {
            var client = httpClientFactory.CreateClient("autohub_jwks");
            var json = client.GetStringAsync(_jwksUrl).GetAwaiter().GetResult();
            _jwks = new JsonWebKeySet(json);
            _cacheExpiresUtc = DateTimeOffset.UtcNow.Add(CacheDuration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh JWKS from {JwksUrl}.", _jwksUrl);
            if (_jwks is null)
            {
                throw;
            }
        }
    }
}
