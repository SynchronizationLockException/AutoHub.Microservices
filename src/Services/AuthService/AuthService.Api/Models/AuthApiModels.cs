namespace AuthService.Api.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RevokeRequest(string RefreshToken);

public sealed record TokenResponse(string AccessToken, string RefreshToken, string Role);
