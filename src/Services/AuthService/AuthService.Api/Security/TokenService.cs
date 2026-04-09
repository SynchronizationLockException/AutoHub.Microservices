using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace AuthService.Api.Security;

public sealed class TokenService(IConfiguration configuration, RsaJwtSigningKeys keys)
{
    public string CreateAccessToken(string username, string role)
    {
        var issuer = configuration["Jwt:Issuer"]!;
        var audience = configuration["Jwt:Audience"]!;
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims:
            [
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: keys.SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshTokenIssue IssueRefreshToken()
    {
        var secretBytes = RandomNumberGenerator.GetBytes(64);
        return new RefreshTokenIssue(
            Convert.ToBase64String(secretBytes),
            RefreshTokenHasher.HashSecretBytes(secretBytes));
    }
}

public sealed record RefreshTokenIssue(string Secret, byte[] Hash);
