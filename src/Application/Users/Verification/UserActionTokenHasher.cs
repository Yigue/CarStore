using System.Security.Cryptography;
using System.Text;

namespace Application.Users.Verification;

/// <summary>
/// sfc A4.1 (design C16): the emailed secret is 32 CSPRNG bytes in base64url; only its SHA-256 digest is
/// persisted. A fast hash is enough because the input is already high-entropy.
/// </summary>
public static class UserActionTokenHasher
{
    public static string NewToken() => 
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
