using AuthService.Api.Data;
using AuthService.Api.Models;
using AuthService.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/token", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("login");

        app.MapPost("/api/auth/refresh", RefreshAsync).AllowAnonymous();
        app.MapPost("/api/auth/revoke", RevokeAsync).AllowAnonymous();
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        AuthDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(
            x => x.Username == request.Username.ToLowerInvariant(),
            ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        var accessToken = tokens.CreateAccessToken(user.Username, user.Role);
        var refresh = tokens.IssueRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new TokenResponse(accessToken, refresh.Secret, user.Role));
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        AuthDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        if (!RefreshTokenHasher.TryParseAndHash(request.RefreshToken, out var hash))
        {
            return Results.Unauthorized();
        }

        var refresh = await db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (refresh is null || refresh.IsRevoked || refresh.ExpiresAtUtc <= DateTime.UtcNow || refresh.User is null)
        {
            return Results.Unauthorized();
        }

        refresh.IsRevoked = true;
        var newRefresh = tokens.IssueRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = refresh.UserId,
            TokenHash = newRefresh.Hash,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14)
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new TokenResponse(
            tokens.CreateAccessToken(refresh.User.Username, refresh.User.Role),
            newRefresh.Secret,
            refresh.User.Role));
    }

    private static async Task<IResult> RevokeAsync(
        [FromBody] RevokeRequest request,
        AuthDbContext db,
        CancellationToken ct)
    {
        if (!RefreshTokenHasher.TryParseAndHash(request.RefreshToken, out var hash))
        {
            return Results.NotFound();
        }

        var refresh = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (refresh is null)
        {
            return Results.NotFound();
        }

        refresh.IsRevoked = true;
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }
}
