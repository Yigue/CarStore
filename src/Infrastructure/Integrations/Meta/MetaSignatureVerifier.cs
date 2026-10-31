using System;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Integrations.Meta;

/// <summary>
/// sfc C1.4 / B13: verifies <c>X-Hub-Signature-256</c> — the HMAC-SHA256-over-the-raw-body scheme Meta uses across
/// its webhook products (Lead Ads, WhatsApp Business, Messenger, ...), keyed by the app secret.
/// </summary>
public static class MetaSignatureVerifier
{
    private const string Sha256Prefix = "sha256=";

    public static bool IsValid(ReadOnlySpan<byte> payload, string? signatureHeader, string? appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(appSecret))
        {
            return false;
        }

        if (!signatureHeader.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string expectedHex = signatureHeader[Sha256Prefix.Length..];

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] key = Encoding.UTF8.GetBytes(appSecret);
        byte[] computed = HMACSHA256.HashData(key, payload);

        return computed.Length == expected.Length && CryptographicOperations.FixedTimeEquals(computed, expected);
    }
}
