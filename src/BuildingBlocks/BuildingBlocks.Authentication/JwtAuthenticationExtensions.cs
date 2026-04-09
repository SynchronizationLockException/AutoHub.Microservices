using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Authentication;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddAutoHubJwtBearer(this IServiceCollection services, IConfiguration configuration)
    {
        var jwksUrl = configuration["Jwt:JwksUrl"];
        var symmetricKey = configuration["Jwt:Key"];

        if (!string.IsNullOrWhiteSpace(jwksUrl))
        {
            services.AddHttpClient("autohub_jwks", client => client.Timeout = TimeSpan.FromSeconds(15));
            services.AddSingleton<JwksSigningKeyCache>();
            services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, ConfigureAutoHubJwtBearerFromJwks>();
        }
        else if (!string.IsNullOrWhiteSpace(symmetricKey))
        {
            services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, ConfigureAutoHubJwtBearerSymmetric>();
        }
        else
        {
            throw new InvalidOperationException(
                "Configure Jwt:JwksUrl (recommended, RSA + JWKS) or Jwt:Key (symmetric, for tests only).");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        return services;
    }
}
