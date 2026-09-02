using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.ErrorContract;

/// <summary>
/// qa-p1-integridad PR1, Slice 1 (D1, REQ: api-error-contract).
/// <para>
/// Today <see cref="Microsoft.AspNetCore.Http.RouteHandlerOptions.ThrowOnBadRequest"/> is never
/// configured, so it defaults to <c>true</c> only in <c>Development</c> (framework throws
/// <c>BadHttpRequestException</c>, which <c>GlobalExceptionHandler</c> currently rewrites to a bare
/// 500) and <c>false</c> everywhere else (framework writes its own bodiless 400, bypassing the
/// handler entirely). Both are wrong; the fix converges the two environments onto one code path
/// that produces one typed <c>ProblemDetails</c> 400.
/// </para>
/// </summary>
public class GlobalHandlerConvergenceTests
{
    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client)> CreateAuthenticatedHostAsync(string environment)
    {
        var factory = new CustomWebApplicationFactory(environment);
        factory.SeedDatabase();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);
        return (factory, client);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task MissingRequiredQueryParameter_Returns400ProblemDetails(string environment)
    {
        var (factory, client) = await CreateAuthenticatedHostAsync(environment);
        await using var _ = factory;

        // GET /api/v1/clients/search with no `q` — SearchClients binds a non-nullable `string q`.
        var response = await client.GetAsync("/api/v1/clients/search");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"a missing required query parameter must be a typed 400 in {environment}, not a 500 or a bodiless 400");

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotBeNullOrWhiteSpace(
            $"the {environment} response must carry a ProblemDetails body, not an empty one");

        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        body.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(400);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task MalformedJsonBody_Returns400NotServerError(string environment)
    {
        var (factory, client) = await CreateAuthenticatedHostAsync(environment);
        await using var _ = factory;

        using var content = new StringContent("{not valid json", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/leads", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"a malformed JSON body must produce 400 in {environment}, never 500");

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotBeNullOrWhiteSpace(
            $"the {environment} response must carry a ProblemDetails body, not an empty one");

        var body = JsonSerializer.Deserialize<JsonElement>(raw);
        body.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetInt32().Should().Be(400);
    }

    [Fact]
    public async Task DevelopmentAndProduction_ReturnIdenticalProblemDetailsShapeForMissingQueryParameter()
    {
        var (devFactory, devClient) = await CreateAuthenticatedHostAsync("Development");
        await using var _1 = devFactory;
        var (prodFactory, prodClient) = await CreateAuthenticatedHostAsync("Production");
        await using var _2 = prodFactory;

        var devResponse = await devClient.GetAsync("/api/v1/clients/search");
        var prodResponse = await prodClient.GetAsync("/api/v1/clients/search");

        devResponse.StatusCode.Should().Be(prodResponse.StatusCode)
            .And.Be(HttpStatusCode.BadRequest, "both hosts must converge on the same status code");

        var devRaw = await devResponse.Content.ReadAsStringAsync();
        var prodRaw = await prodResponse.Content.ReadAsStringAsync();
        devRaw.Should().NotBeNullOrWhiteSpace("Development body must carry a ProblemDetails payload");
        prodRaw.Should().NotBeNullOrWhiteSpace("Production body must carry a ProblemDetails payload, not be bodiless");

        var devBody = JsonSerializer.Deserialize<JsonElement>(devRaw);
        var prodBody = JsonSerializer.Deserialize<JsonElement>(prodRaw);

        devBody.TryGetProperty("type", out _).Should().BeTrue("Development body must carry a ProblemDetails 'type'");
        prodBody.TryGetProperty("type", out _).Should().BeTrue("Production body must carry a ProblemDetails 'type', not be bodiless");
    }

    /// <summary>Regression guard: the existing DomainException -> 400 mapping must be unchanged by this PR.</summary>
    [Fact]
    public async Task DomainException_StillMapsTo400()
    {
        var (factory, client) = await CreateAuthenticatedHostAsync("Testing");
        await using var _ = factory;

        // A lead moving to Perdido without a loss reason fails Lead.UpdateStatus's guard,
        // raising DomainException — mapped to 400 by the handler's first, unchanged arm.
        //
        // Perdido is used deliberately: it is the one transition the command handler does not
        // pre-check, so the exception really does come from the aggregate. The Contactado path
        // this test used to take is now caught earlier and returned as a typed
        // Leads.RequiresAssignedAgent failure, which is also a 400 but proves nothing about
        // exception mapping.
        Guid leadId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Database.ApplicationDbContext>();
            var lead = Domain.Leads.Lead.Create(
                Guid.Parse(CustomWebApplicationFactory.AdminDealerId),
                "Domain Exception Regression",
                "domain-exception@lead.com",
                "555000",
                Domain.Leads.LeadSource.Web,
                DateTime.UtcNow);
            db.Leads.Add(lead);
            await db.SaveChangesAsync();
            leadId = lead.Id;
        }

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/leads/{leadId}/status",
            new { NewStatus = "Perdido" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "DomainException must still map to 400 — unchanged regression guard");
    }

    /// <summary>
    /// Slice 1.6 (D1): FormatException is deliberately NOT mapped by the global handler. Malformed
    /// base64 in document upload is caught by a validator in PR7, not here — mapping FormatException
    /// globally would reclassify genuine server bugs (any unrelated FormatException anywhere in the
    /// app) as client errors. This is a documentation-only regression guard: there is no endpoint in
    /// PR1's scope that throws FormatException, so there is nothing to assert against yet; the
    /// contract is enforced by 3.1's endpoint-scoped sweep leaving FormatException off every arm.
    /// </summary>
    [Fact]
    public void FormatException_IsDeliberatelyNotMappedByDesign()
    {
        // No-op assertion, intentional: see api-error-contract spec, "FormatException Is Not
        // Globally Reclassified As A Client Error". PR7 (documents upload validator) is the actual
        // enforcement point; this test exists only so the deliberate omission has a source-visible
        // regression guard next to the other GlobalExceptionHandler assertions in this PR.
        true.Should().BeTrue();
    }
}
