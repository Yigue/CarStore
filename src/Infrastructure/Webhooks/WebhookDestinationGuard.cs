using System.Net;
using System.Net.Sockets;
using Application.Abstractions.Webhooks;

namespace Infrastructure.Webhooks;

/// <summary>
/// Default implementation of <see cref="IWebhookDestinationGuard"/>.
/// Blocks loopback, RFC 1918 private networks, link-local / cloud metadata (169.254.169.254),
/// carrier-grade NAT, IPv6 ULA, and IPv4-mapped IPv6 equivalents.
/// Enforces a 2-second DNS resolution timeout and fails closed on any resolution failure.
/// </summary>
public class WebhookDestinationGuard : IWebhookDestinationGuard
{
    private static readonly TimeSpan DefaultDnsTimeout = TimeSpan.FromSeconds(2);
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _dnsLookup;

    /// <summary>
    /// Initializes a new instance of <see cref="WebhookDestinationGuard"/> using system DNS resolution.
    /// </summary>
    public WebhookDestinationGuard()
        : this((host, ct) => Dns.GetHostAddressesAsync(host, ct))
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="WebhookDestinationGuard"/> with a custom DNS lookup delegate (for testing).
    /// </summary>
    /// <param name="dnsLookup">Delegate to perform hostname resolution.</param>
    public WebhookDestinationGuard(Func<string, CancellationToken, Task<IPAddress[]>> dnsLookup)
    {
        _dnsLookup = dnsLookup ?? ((host, ct) => Dns.GetHostAddressesAsync(host, ct));
    }

    /// <inheritdoc />
    public async Task<bool> ValidateDestinationAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri is null || !uri.IsAbsoluteUri)
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        string host = uri.DnsSafeHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        // If host is an IP literal, validate directly without DNS
        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return !IsBlocked(ipAddress);
        }

        // Hostname resolution with 2-second timeout and fail-closed policy
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(DefaultDnsTimeout);

            IPAddress[] addresses = await _dnsLookup(host, cts.Token);
            if (addresses is null || addresses.Length == 0)
            {
                return false;
            }

            foreach (var ip in addresses)
            {
                if (IsBlocked(ip))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            // Fail closed on DNS lookup errors, NXDOMAIN, network failures, or timeout
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> ValidateDestinationAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Task.FromResult(false);
        }

        return ValidateDestinationAsync(uri, cancellationToken);
    }

    /// <inheritdoc />
    public bool IsBlocked(IPAddress ipAddress)
    {
        if (ipAddress is null)
        {
            return true;
        }

        // Normalize IPv4-mapped IPv6 addresses (e.g. ::ffff:127.0.0.1 -> 127.0.0.1)
        if (ipAddress.IsIPv4MappedToIPv6)
        {
            ipAddress = ipAddress.MapToIPv4();
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
        {
            return IsBlockedIPv4(ipAddress);
        }

        if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return IsBlockedIPv6(ipAddress);
        }

        // Unknown or unsupported address family -> block
        return true;
    }

    private static bool IsBlockedIPv4(IPAddress ip)
    {
        byte[] b = ip.GetAddressBytes();

        // 0.0.0.0/8 (Current network)
        if (b[0] == 0)
        {
            return true;
        }

        // 127.0.0.0/8 (Loopback)
        if (b[0] == 127)
        {
            return true;
        }

        // 10.0.0.0/8 (RFC 1918 Private)
        if (b[0] == 10)
        {
            return true;
        }

        // 172.16.0.0/12 (RFC 1918 Private: 172.16.0.0 - 172.31.255.255)
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
        {
            return true;
        }

        // 192.168.0.0/16 (RFC 1918 Private)
        if (b[0] == 192 && b[1] == 168)
        {
            return true;
        }

        // 100.64.0.0/10 (Carrier-Grade NAT / Shared Address Space RFC 6598: 100.64.0.0 - 100.127.255.255)
        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
        {
            return true;
        }

        // 169.254.0.0/16 (Link-Local / Cloud Metadata RFC 3927)
        if (b[0] == 169 && b[1] == 254)
        {
            return true;
        }

        // 192.0.0.0/24 (IETF Protocol Assignments)
        if (b[0] == 192 && b[1] == 0 && b[2] == 0)
        {
            return true;
        }

        // 192.0.2.0/24 (TEST-NET-1)
        if (b[0] == 192 && b[1] == 0 && b[2] == 2)
        {
            return true;
        }

        // 198.18.0.0/15 (Benchmarking)
        if (b[0] == 198 && (b[1] == 18 || b[1] == 19))
        {
            return true;
        }

        // 198.51.100.0/24 (TEST-NET-2)
        if (b[0] == 198 && b[1] == 51 && b[2] == 100)
        {
            return true;
        }

        // 203.0.113.0/24 (TEST-NET-3)
        if (b[0] == 203 && b[1] == 0 && b[2] == 113)
        {
            return true;
        }

        // 224.0.0.0/4 (Multicast: 224.0.0.0 - 239.255.255.255)
        // 240.0.0.0/4 (Reserved for future use & 255.255.255.255 broadcast)
        if (b[0] >= 224)
        {
            return true;
        }

        return false;
    }

    private static bool IsBlockedIPv6(IPAddress ip)
    {
        // Loopback (::1)
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.IPv6Loopback))
        {
            return true;
        }

        // Unspecified (::)
        if (ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        byte[] b = ip.GetAddressBytes();

        // Link-Local (fe80::/10): first 10 bits must be 1111 1110 10xx xxxx (0xfe80 - 0xfebf)
        if (ip.IsIPv6LinkLocal || (b[0] == 0xfe && (b[1] & 0xc0) == 0x80))
        {
            return true;
        }

        // Unique Local Address ULA (fc00::/7): first 7 bits must be 1111 110x (0xfc or 0xfd)
        if ((b[0] & 0xfe) == 0xfc)
        {
            return true;
        }

        // Deprecated Site-Local (fec0::/10): first 10 bits 1111 1110 11xx xxxx
        if (ip.IsIPv6SiteLocal || (b[0] == 0xfe && (b[1] & 0xc0) == 0xc0))
        {
            return true;
        }

        // Multicast (ff00::/8)
        if (ip.IsIPv6Multicast || b[0] == 0xff)
        {
            return true;
        }

        // Deprecated IPv4-compatible IPv6 (::x.x.x.x where bytes 0..11 are all zero)
        bool isCompatible = true;
        for (int i = 0; i < 12; i++)
        {
            if (b[i] != 0)
            {
                isCompatible = false;
                break;
            }
        }

        if (isCompatible)
        {
            var v4 = new IPAddress(new ReadOnlySpan<byte>(b, 12, 4));
            return IsBlockedIPv4(v4);
        }

        return false;
    }
}
