using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Authentication;

public sealed class JwksSigningKeyCache : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<JwksSigningKeyCache> _logger;
    private readonly string _jwksUrl;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private JsonWebKeySet? _jwks;
    private IReadOnlyList<SecurityKey> _signingKeys = [];
    private DateTimeOffset _cacheExpiresUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _circuitOpenUntilUtc = DateTimeOffset.MinValue;
    private int _consecutiveFailures;
    private int _refreshScheduled;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(1);

    public JwksSigningKeyCache(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<JwksSigningKeyCache> logger,
        IHostApplicationLifetime appLifetime)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jwksUrl = configuration["Jwt:JwksUrl"]
            ?? throw new InvalidOperationException("Jwt:JwksUrl is required when using JWKS validation.");
        appLifetime.ApplicationStopping.Register(() => _cancellationTokenSource.Cancel());
    }

    public IEnumerable<SecurityKey> ResolveSigningKeys(string? kid)
    {
        TriggerRefreshIfNeeded();
        IReadOnlyList<SecurityKey> keys;
        lock (_lock)
        {
            keys = _signingKeys;
        }

        if (string.IsNullOrEmpty(kid))
        {
            return keys;
        }

        return keys.Where(k => string.Equals(k.KeyId, kid, StringComparison.OrdinalIgnoreCase));
    }

    public void Invalidate()
    {
        lock (_lock)
        {
            _cacheExpiresUtc = DateTimeOffset.MinValue;
            _circuitOpenUntilUtc = DateTimeOffset.MinValue;
        }

        TriggerRefreshIfNeeded();
    }

    private void TriggerRefreshIfNeeded()
    {
        var now = DateTimeOffset.UtcNow;
        var shouldRefresh = false;
        lock (_lock)
        {
            if (_jwks is null || now >= _cacheExpiresUtc)
            {
                shouldRefresh = now >= _circuitOpenUntilUtc;
            }
        }

        if (!shouldRefresh)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _refreshScheduled, 1, 0) != 0)
        {
            return;
        }

        FireAndForgetRefresh();
    }

    private void FireAndForgetRefresh()
    {
        var task = RefreshInBackgroundAsync();
        task.ContinueWith(
            t => _logger.LogError(t.Exception, "JWKS background refresh crashed."),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task RefreshInBackgroundAsync()
    {
        try
        {
            await _refreshGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = DateTimeOffset.UtcNow;
                lock (_lock)
                {
                    if (_jwks is not null && now < _cacheExpiresUtc)
                    {
                        return;
                    }

                    if (now < _circuitOpenUntilUtc)
                    {
                        return;
                    }
                }

                var client = _httpClientFactory.CreateClient("autohub_jwks");
                var json = await client.GetStringAsync(_jwksUrl, _cancellationTokenSource.Token).ConfigureAwait(false);
                var jwks = new JsonWebKeySet(json);
                var signingKeys = jwks.GetSigningKeys().ToArray();

                lock (_lock)
                {
                    _jwks = jwks;
                    _signingKeys = signingKeys;
                    _cacheExpiresUtc = DateTimeOffset.UtcNow.Add(CacheDuration);
                    _circuitOpenUntilUtc = DateTimeOffset.MinValue;
                    _consecutiveFailures = 0;
                }
            }
            catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
            {
                // App is shutting down; avoid treating cancellation as a refresh failure.
            }
            catch (Exception ex)
            {
                TimeSpan backoff;
                lock (_lock)
                {
                    _consecutiveFailures++;
                    var rawSeconds = Math.Pow(2, Math.Min(_consecutiveFailures, 6));
                    backoff = TimeSpan.FromSeconds(Math.Min(rawSeconds, MaxBackoff.TotalSeconds));
                    _circuitOpenUntilUtc = DateTimeOffset.UtcNow.Add(backoff);
                }

                _logger.LogWarning(
                    ex,
                    "Failed to refresh JWKS from {JwksUrl}. Circuit open for {BackoffSeconds} seconds.",
                    _jwksUrl,
                    backoff.TotalSeconds);
            }
            finally
            {
                _refreshGate.Release();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _refreshScheduled, 0);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _refreshGate.Dispose();
    }
}
