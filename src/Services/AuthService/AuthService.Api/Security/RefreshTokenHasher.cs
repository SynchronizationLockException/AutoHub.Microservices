using System.Security.Cryptography;

namespace AuthService.Api.Security;

public static class RefreshTokenHasher
{
    public static byte[] HashSecretBytes(ReadOnlySpan<byte> secretBytes) =>
        SHA256.HashData(secretBytes);

    public static bool TryParseAndHash(string refreshTokenBase64, out byte[] hash)
    {
        hash = Array.Empty<byte>();
        try
        {
            var bytes = Convert.FromBase64String(refreshTokenBase64);
            if (bytes.Length < 32)
            {
                return false;
            }

            hash = HashSecretBytes(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
