using System.Net;
using FluentAssertions;
using WebApiTests;

namespace WebApiTests.IntegrationTests;

/// <summary>
/// Integration tests for tenant safety: spec `tenant-safety-default-deny`.
/// task 1.6.2: anonymous request with unknown host must return 404 with no payload.
/// </summary>
public sealed class TenantSafetyTests
{
    /// <summary>
    /// spec: tenant-safety-default-deny §host miss
    /// When the Host header does not resolve to any DealerSettings row,
    /// TenantResolutionMiddleware must short-circuit with 404 and return no payload.
    /// </summary>
    [Fact]
    public async Task AnonymousRequest_WithUnknownHost_Returns404()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();

        // Use a fresh client so we can set a custom Host header.
        // WebApplicationFactory honours the Host header from DefaultRequestHeaders.
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Host = "unknown.carstore.com";

        // Act
        var response = await client.GetAsync("/api/v1/cars/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "TenantResolutionMiddleware must short-circuit with 404 when the host is not registered");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("cars",
            because: "no payload should be returned for an unresolved tenant");
    }
}
