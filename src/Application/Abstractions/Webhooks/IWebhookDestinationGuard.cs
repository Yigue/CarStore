using System.Net;

namespace Application.Abstractions.Webhooks;

/// <summary>
/// Guard to validate outgoing webhook destinations against Server-Side Request Forgery (SSRF).
/// Enforces blocking of loopback, RFC 1918 private networks, link-local / cloud metadata (e.g. 169.254.169.254),
/// carrier-grade NAT, IPv6 ULA, and their IPv4-mapped equivalents.
/// </summary>
public interface IWebhookDestinationGuard
{
    /// <summary>
    /// Validates whether the specified URI is an allowed public webhook destination.
    /// Fails closed (returns false) if the URI is not absolute HTTP(S), cannot be resolved,
    /// times out (2s limit), or resolves to any restricted IP address.
    /// </summary>
    /// <param name="uri">The destination URI to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the destination is safe and allowed; false otherwise.</returns>
    Task<bool> ValidateDestinationAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the specified URL string is an allowed public webhook destination.
    /// </summary>
    /// <param name="url">The destination URL to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the destination is safe and allowed; false otherwise.</returns>
    Task<bool> ValidateDestinationAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an individual IP address is in a blocked or restricted range.
    /// </summary>
    /// <param name="ipAddress">The IP address to inspect.</param>
    /// <returns>True if the IP address is blocked/restricted; false if it is a public, allowed address.</returns>
    bool IsBlocked(IPAddress ipAddress);
}
