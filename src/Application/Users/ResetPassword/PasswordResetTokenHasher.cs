using System.Security.Cryptography;
using System.Text;

namespace Application.Users.ResetPassword;

/// <summary>
/// The reset token is a 256-bit random secret emailed to the user. Only its SHA-256 digest is
/// persisted, so a leaked database row cannot be replayed as a token. A fast hash is enough
/// here: the input is already high-entropy, there is nothing to brute-force.
/// </summary>
public static class PasswordResetTokenHasher
{
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
