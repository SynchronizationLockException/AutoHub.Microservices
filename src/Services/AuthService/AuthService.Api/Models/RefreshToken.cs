namespace AuthService.Api.Models;

public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public AuthUser? User { get; init; }

    public byte[] TokenHash { get; init; } = Array.Empty<byte>();

    public DateTime ExpiresAtUtc { get; init; }
    public bool IsRevoked { get; set; }
}
