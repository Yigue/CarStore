using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Infrastructure.Webhooks;
using Xunit;

namespace InfrastructureTests.Webhooks;

public class WebhookDestinationGuardTests
{
    private readonly WebhookDestinationGuard _guard = new();

    // ─── IP address classification: Blocked ranges ─────────────────────────

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("127.255.255.255")]
    public void IsBlocked_ShouldReturnTrue_ForIPv4Loopback(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("0.1.2.3")]
    public void IsBlocked_ShouldReturnTrue_ForIPv4CurrentNetwork(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("192.168.254.254")]
    public void IsBlocked_ShouldReturnTrue_ForRFC1918PrivateRanges(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    public void IsBlocked_ShouldReturnTrue_ForCarrierGradeNat(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("169.254.169.254")] // AWS, Azure, GCP metadata service
    [InlineData("169.254.0.1")]
    [InlineData("169.254.255.255")]
    public void IsBlocked_ShouldReturnTrue_ForIPv4LinkLocalAndMetadata(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("255.255.255.255")] // Broadcast
    [InlineData("224.0.0.1")]       // Multicast
    [InlineData("240.0.0.1")]       // Reserved
    public void IsBlocked_ShouldReturnTrue_ForBroadcastMulticastAndReservedIPv4(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_ShouldReturnTrue_ForIPv6Loopback()
    {
        var ip = IPAddress.Parse("::1");
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_ShouldReturnTrue_ForIPv6Unspecified()
    {
        var ip = IPAddress.Parse("::");
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("fe80::1")]
    [InlineData("fe80::dead:beef")]
    [InlineData("febf::1")]
    public void IsBlocked_ShouldReturnTrue_ForIPv6LinkLocal(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("fc00::1")]
    [InlineData("fd00::1")]
    [InlineData("fd12:3456:789a::1")]
    public void IsBlocked_ShouldReturnTrue_ForIPv6UniqueLocalAddresses(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("fec0::1")] // Deprecated IPv6 site-local
    [InlineData("ff02::1")] // IPv6 multicast
    public void IsBlocked_ShouldReturnTrue_ForIPv6SiteLocalAndMulticast(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:172.16.0.1")]
    [InlineData("::ffff:192.168.1.1")]
    public void IsBlocked_ShouldReturnTrue_ForIPv4MappedIPv6RestrictedAddresses(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeTrue();
    }

    // ─── IP address classification: Allowed public IPs ────────────────────

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("172.15.255.255")] // Just below 172.16.0.0
    [InlineData("172.32.0.1")]     // Just above 172.31.255.255
    [InlineData("100.63.255.255")] // Just below CGNAT
    [InlineData("100.128.0.1")]    // Just above CGNAT
    [InlineData("169.253.255.255")]// Just below 169.254.0.0
    [InlineData("169.255.0.1")]    // Just above 169.254.255.255
    [InlineData("2001:4860:4860::8888")] // Google Public DNS IPv6
    [InlineData("2606:4700:4700::1111")] // Cloudflare DNS IPv6
    public void IsBlocked_ShouldReturnFalse_ForPublicIPAddresses(string ipString)
    {
        var ip = IPAddress.Parse(ipString);
        _guard.IsBlocked(ip).Should().BeFalse();
    }

    // ─── ValidateDestinationAsync: URI & URL validation ──────────────────

    [Theory]
    [InlineData("http://127.0.0.1/webhook")]
    [InlineData("http://127.0.0.1:8080/hook")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://10.0.0.5/hook")]
    [InlineData("http://192.168.1.100/webhook")]
    [InlineData("http://[::1]/webhook")]
    [InlineData("http://[::ffff:127.0.0.1]/webhook")]
    public async Task ValidateDestinationAsync_ShouldReturnFalse_ForRestrictedIPLiterals(string url)
    {
        var result = await _guard.ValidateDestinationAsync(url);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("ftp://example.com/webhook")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com/")]
    [InlineData("not-a-valid-url")]
    public async Task ValidateDestinationAsync_ShouldReturnFalse_ForNonHttpSchemesOrInvalidUrls(string url)
    {
        var result = await _guard.ValidateDestinationAsync(url);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDestinationAsync_ShouldReturnTrue_ForValidPublicIPLiteral()
    {
        var result = await _guard.ValidateDestinationAsync("https://93.184.216.34/webhook");
        result.Should().BeTrue();
    }

    // ─── DNS Resolution Behavior & Fail-Closed ────────────────────────────

    [Fact]
    public async Task ValidateDestinationAsync_ShouldReturnFalse_WhenHostResolvesToRestrictedIP()
    {
        var guardWithMockDns = new WebhookDestinationGuard((_, _) =>
            Task.FromResult(new[] { IPAddress.Parse("127.0.0.1") }));

        var result = await guardWithMockDns.ValidateDestinationAsync("https://local-rebinding.example.com/webhook");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDestinationAsync_ShouldReturnFalse_WhenAnyResolvedIPIsRestricted()
    {
        // Multi-homed DNS response where one IP is public and another is private/internal
        var guardWithMockDns = new WebhookDestinationGuard((_, _) =>
            Task.FromResult(new[]
            {
                IPAddress.Parse("8.8.8.8"),
                IPAddress.Parse("10.0.0.1")
            }));

        var result = await guardWithMockDns.ValidateDestinationAsync("https://split-dns.example.com/webhook");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDestinationAsync_ShouldReturnTrue_WhenAllResolvedIPsArePublic()
    {
        var guardWithMockDns = new WebhookDestinationGuard((_, _) =>
            Task.FromResult(new[]
            {
                IPAddress.Parse("93.184.216.34"),
                IPAddress.Parse("2001:4860:4860::8888")
            }));

        var result = await guardWithMockDns.ValidateDestinationAsync("https://public-api.example.com/webhook");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateDestinationAsync_ShouldReturnFalse_WhenDnsResolutionFails_FailClosed()
    {
        var guardWithMockDns = new WebhookDestinationGuard((_, _) =>
            throw new SocketException((int)SocketError.HostNotFound));

        var result = await guardWithMockDns.ValidateDestinationAsync("https://nonexistent-domain-nxdomain.test/webhook");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDestinationAsync_ShouldReturnFalse_WhenDnsReturnsNoAddresses_FailClosed()
    {
        var guardWithMockDns = new WebhookDestinationGuard((_, _) =>
            Task.FromResult(Array.Empty<IPAddress>()));

        var result = await guardWithMockDns.ValidateDestinationAsync("https://empty-dns.example.com/webhook");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDestinationAsync_ShouldReturnFalse_WhenDnsLookupTimesOut_FailClosed()
    {
        var guardWithMockDns = new WebhookDestinationGuard(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new[] { IPAddress.Parse("8.8.8.8") };
        });

        // The guard has a 2-second timeout and fails closed
        var result = await guardWithMockDns.ValidateDestinationAsync("https://slow-dns.example.com/webhook");

        result.Should().BeFalse();
    }
}
