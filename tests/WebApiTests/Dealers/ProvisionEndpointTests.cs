using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Security;
using Domain.DealerSettings;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebApiTests.Fakes;

namespace WebApiTests.Dealers;

public class ProvisionEndpointTests
{
    private sealed record ProvisionResponse(Guid DealerId, Guid AdminUserId, string Subdomain);

    // spr PR 3.3 (B10): CaptchaEndpointFilter requires a non-empty token on every request. In the
    // "Testing" environment (default CustomWebApplicationFactory) no Captcha:SecretKey is configured,
    // so ICaptchaVerifier resolves to NoOpCaptchaVerifier — any non-empty token is accepted.
    private const string ValidCaptchaToken = "test-captcha-token";

    private static object ValidBody(string subdomain = "automotors", string email = "admin@automotors.com") => new
    {
        DealerName = "Automotors del Sur",
        Subdomain = subdomain,
        AdminEmail = email,
        AdminPassword = "Sup3r$ecret!",
        AdminFirstName = "Ana",
        AdminLastName = "García",
        AcceptedTermsVersion = "2026-10-01",
        CaptchaToken = ValidCaptchaToken
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
        settings.OwnerUserId.Should().Be(body.AdminUserId,
            "sfc C17: the first administrator is the owner from the very first row");

        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == body.AdminUserId);
        user.Should().NotBeNull();
        user!.RoleId.Should().NotBeEmpty();
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
            AdminLastName = "B",
            AcceptedTermsVersion = "2026-10-01",
            CaptchaToken = ValidCaptchaToken
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

    // spr PR 3.3 (B10) — task 3.3.1 [RED]: CaptchaEndpointFilter must reject the request BEFORE the
    // MediatR handler runs, so neither DealerSettings, Users nor DealerSubscriptions gain a row.
    [Fact]
    public async Task Provision_ReturnsBadRequest_WhenCaptchaTokenMissing_AndCreatesNoRows()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var body = new
        {
            DealerName = "Automotors del Sur",
            Subdomain = "nocaptcha",
            AdminEmail = "admin@nocaptcha.com",
            AdminPassword = "Sup3r$ecret!",
            AdminFirstName = "Ana",
            AdminLastName = "García",
            AcceptedTermsVersion = "2026-10-01"
            // CaptchaToken intentionally omitted.
        };

        var response = await client.PostAsJsonAsync("/api/v1/dealers/provision", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoRowsPersistedAsync(factory, "nocaptcha");
    }

    [Fact]
    public async Task Provision_ReturnsBadRequest_WhenCaptchaTokenInvalid_AndCreatesNoRows()
    {
        await using var factory = new CaptchaStubWebApplicationFactory(CaptchaVerificationResult.Invalid);
        var client = factory.CreateClient();

        var body = ValidBody(subdomain: "badcaptcha");
        var response = await client.PostAsJsonAsync("/api/v1/dealers/provision", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await AssertNoRowsPersistedAsync(factory, "badcaptcha");
    }

    [Fact]
    public async Task Provision_ReturnsServiceUnavailable_WhenCaptchaProviderUnavailable_AndCreatesNoRows()
    {
        await using var factory = new CaptchaStubWebApplicationFactory(CaptchaVerificationResult.Unavailable);
        var client = factory.CreateClient();

        var body = ValidBody(subdomain: "downcaptcha");
        var response = await client.PostAsJsonAsync("/api/v1/dealers/provision", body);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await AssertNoRowsPersistedAsync(factory, "downcaptcha");
    }

    /// <summary>
    /// CustomWebApplicationFactory boots in "Testing", which runs <c>app.SeedTestData()</c>
    /// (Program.cs) — the DB already has demo rows before any request, so a blanket zero-count
    /// assertion is wrong. Checking by this test's unique <c>subdomain</c> (same pattern as
    /// <c>Provision_ReturnsConflict_OnDuplicateSubdomain</c> above) is precise regardless of seed data.
    /// </summary>
    private static async Task AssertNoRowsPersistedAsync(CustomWebApplicationFactory factory, string subdomain)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.DealerSettings.IgnoreQueryFilters().AnyAsync(s => s.HostName == subdomain))
            .Should().BeFalse($"no DealerSettings row should exist after a rejected captcha for '{subdomain}'");
    }

    /// <summary>Boots the normal Testing host but swaps <see cref="ICaptchaVerifier"/> for a deterministic stub.</summary>
    private sealed class CaptchaStubWebApplicationFactory(CaptchaVerificationResult result) : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICaptchaVerifier>();
                services.AddSingleton<ICaptchaVerifier>(new StubCaptchaVerifier { Result = result });
            });
        }
    }
}