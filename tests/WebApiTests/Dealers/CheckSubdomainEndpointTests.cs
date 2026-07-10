using System.Net;
using System.Net.Http.Json;

namespace WebApiTests.Dealers;

public class CheckSubdomainEndpointTests
{
    private sealed record AvailabilityResponse(bool Available, string? Reason, bool Reserved);

    [Fact]
    public async Task CheckSubdomain_ReturnsAvailable_WhenUnused()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dealers/check-subdomain?subdomain=freshslug");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AvailabilityResponse>(IntegrationTestHelpers.JsonOptions);
        body!.Available.Should().BeTrue();
        body.Reserved.Should().BeFalse();
        body.Reason.Should().BeNull();
    }

    [Fact]
    public async Task CheckSubdomain_ReturnsNotAvailable_WhenReserved()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dealers/check-subdomain?subdomain=admin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AvailabilityResponse>(IntegrationTestHelpers.JsonOptions);
        body!.Available.Should().BeFalse();
        body.Reserved.Should().BeTrue();
        body.Reason.Should().Be("reserved");
    }

    [Fact]
    public async Task CheckSubdomain_ReturnsBadRequest_OnMissingParam()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dealers/check-subdomain");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckSubdomain_CarriesNoStore_Header()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/dealers/check-subdomain?subdomain=freshslug");

        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task CheckSubdomain_IsAnonymous_NoAuthHeader()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.GetAsync("/api/v1/dealers/check-subdomain?subdomain=freshslug2");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}