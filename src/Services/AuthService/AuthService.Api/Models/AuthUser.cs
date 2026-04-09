namespace AuthService.Api.Models;

public sealed class AuthUser
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
