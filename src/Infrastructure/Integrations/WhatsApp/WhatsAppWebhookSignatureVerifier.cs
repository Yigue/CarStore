using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Integrations.WhatsApp;

/// <summary>
/// sfc C0.4 / RN-046: verifies Meta's <c>X-Hub-Signature-256</c> header — HMAC-SHA256 over the raw request body,
/// keyed with the platform Meta App's secret, hex-encoded and prefixed with <c>sha256=</c>. Same shape as the
/// outgoing-webhook HMAC in <c>DispatchWebhookDeliveriesJob.ComputeSignature</c>, applied here to an inbound call.
/// </summary>
public static class WhatsAppWebhookSignatureVerifier
{
    public static bool Verify(string rawBody, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(appSecret))
        {
            return false;
        }

        const string prefix = "sha256=";
        string provided = signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? signatureHeader[prefix.Length..]
            : signatureHeader;

        byte[] key = Encoding.UTF8.GetBytes(appSecret);
        byte[] computedHash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(rawBody));
        string computed = Convert.ToHexString(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(provided.ToLowerInvariant()));
    }
}
