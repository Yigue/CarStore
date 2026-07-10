using System.Net;
using System.Net.Http.Json;
using Domain.DealerSettings;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApiTests.Dealers;

public class ProvisionEndpointTests
{
    private sealed record ProvisionResponse(Guid DealerId, Guid AdminUserId, string Subdomain);

    private static object ValidBody(string subdomain = "automotors", string email = "admin@automotors.com") => new
    {
        DealerName = "Automotors del Sur",
        Subdomain = subdomain,
        AdminEmail = email,
        AdminPassword = "Sup3r$ecret!",
        AdminFirstName = "Ana",
        AdminLastName = "García"
    };

    [Fact]
    public async Task Provision_ReturnsCreated_OnValidAnonymousRequest()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/dealers/provision", ValidBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ProvisionResponse>(IntegrationTestHelpers.JsonOptions);
        body.Should().NotBeNull();
        body!.DealerId.Should().NotBe(Guid.Empty);
        body.AdminUserId.Should().NotBe(Guid.Empty);
        body.Subdomain.Should().Be("automotors");

        // Verify both rows were persisted in the same transaction.
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await context.DealerSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.DealerId == body.DealerId);
        settings.Should().NotBeNull();
        settings!.HostName.Should().Be("automotors");
        settings.Id.Should().Be(body.DealerId,
            "the row PK (Id) and the tenant FK (DealerId) must share the same Guid in the provision path");

        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == body.AdminUserId);
        user.Should().NotBeNull();
        user!.Role.Should().Be(UserRole.Admin);
        user.DealerId.Should().Be(body.DealerId);
    }

    [Fact]
    public async Task Provision_ReturnsBadRequest_OnWeakPassword()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var weak = new
        {
            DealerName = "Foo",
            Subdomain = "weakpw",
            AdminEmail = "admin@foo.com",
            AdminPassword = "password",
            AdminFirstName = "A",
            AdminLastName = "B"
        };

        var response = await client.PostAsJsonAsync("/api/v1/dealers/provision", weak);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Provision_ReturnsBadRequest_OnReservedSubdomain()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var body = ValidBody(subdomain: "admin");

        var response = await client.PostAsJsonAsync("/api/v1/dealers/provision", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Provision_ReturnsBadRequest_OnMalformedSubdomain()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var body = ValidBody(subdomain: "Bad!Slug");

        var response = await client.PostAsJsonAsync("/api/v1/dealers/provision", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Provision_ReturnsConflict_OnDuplicateSubdomain()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/v1/dealers/provision", ValidBody(subdomain: "dupe", email: "first@dupe.com"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/v1/dealers/provision", ValidBody(subdomain: "dupe", email: "second@dupe.com"));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Exactly one DealerSettings row for the duplicate subdomain.
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.DealerSettings.IgnoreQueryFilters().CountAsync(s => s.HostName == "dupe");
        count.Should().Be(1);
    }

    [Fact]
    public async Task Provision_IsAnonymous_NoAuthHeader()
    {
        // No Authorization header set — request must still be processed (200/400, not 401).
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        // Make sure no header leaks in.
        client.DefaultRequestHeaders.Authorization = null;

        var response = await client.PostAsJsonAsync("/api/v1/dealers/provision", ValidBody(subdomain: "anon"));

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
    }
}