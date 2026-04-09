using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json;

namespace AuthService.Api.Security;

public sealed class RsaJwtSigningKeys : IDisposable
{
    private readonly RSA _rsa;
    private readonly string _keyId;

    public RsaJwtSigningKeys(IConfiguration configuration, IHostEnvironment environment, ILogger<RsaJwtSigningKeys> logger)
    {
        var pem = configuration["Jwt:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(pem))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Jwt:PrivateKeyPem must be set in Production (PEM-encoded RSA private key).");
            }

            _rsa = RSA.Create(2048);
            logger.LogWarning("Jwt:PrivateKeyPem is not set; using ephemeral RSA key (tokens invalid after restart).");
        }
        else
        {
            _rsa = RSA.Create();
            _rsa.ImportFromPem(pem);
        }

        _keyId = configuration["Jwt:KeyId"] ?? ComputeDefaultKeyId(_rsa);
        SigningKey = new RsaSecurityKey(_rsa.ExportParameters(true)) { KeyId = _keyId };
        SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256);
    }

    public string KeyId => _keyId;

    public RsaSecurityKey SigningKey { get; }

    public SigningCredentials SigningCredentials { get; }

    private static string ComputeDefaultKeyId(RSA rsa)
    {
        var p = rsa.ExportParameters(false);
        var hash = SHA256.HashData(p.Modulus.AsSpan());
        var prefix = hash.AsSpan(0, Math.Min(8, hash.Length));
        return Base64UrlEncoder.Encode(prefix.ToArray());
    }

    public string GetJwksJson()
    {
        var p = _rsa.ExportParameters(false);
        var key = new Dictionary<string, string?>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["kid"] = _keyId,
            ["alg"] = "RS256",
            ["n"] = Base64UrlEncoder.Encode(p.Modulus!),
            ["e"] = Base64UrlEncoder.Encode(p.Exponent!)
        };

        return JsonSerializer.Serialize(new { keys = new[] { key } });
    }

    public void Dispose() => _rsa.Dispose();
}
