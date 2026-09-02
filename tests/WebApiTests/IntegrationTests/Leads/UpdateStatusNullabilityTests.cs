using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Leads;

/// <summary>
/// qa-p1-integridad PR1, Slice 2 (D2, REQ: crm-lead-pipeline-hardening "newStatus Is Explicitly
/// Required On Lead Status Update"). <c>PATCH /leads/{id}/status</c> bound <c>newStatus</c> as a
/// non-nullable <see cref="Domain.Leads.LeadStatus"/>, so an omitted value silently bound to
/// member 0 (<c>Nuevo</c>) and returned 204 having changed nothing. This PR makes the request and
/// command nullable and adds an explicit "required" validator.
/// </summary>
public class UpdateStatusNullabilityTests
{
    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client, Guid LeadId)> CreateHostWithLeadAsync()
    {
        var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        Guid leadId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lead = Domain.Leads.Lead.Create(
                Guid.Parse(CustomWebApplicationFactory.AdminDealerId),
                "Nullability Test Lead",
                "nullability-test@lead.com",
                "555111",
                Domain.Leads.LeadSource.Web,
                DateTime.UtcNow);
            // Contactado requires an owner, and this class exercises the nullability of
            // newStatus rather than that rule — so give the lead an agent up front.
            var agent = await db.Users
                .IgnoreQueryFilters()
                .FirstAsync(u => u.DealerId == Guid.Parse(CustomWebApplicationFactory.AdminDealerId));
            lead.AssignAgent(agent.Id);

            db.Leads.Add(lead);
            await db.SaveChangesAsync();
            leadId = lead.Id;
        }

        return (factory, client, leadId);
    }

    [Fact]
    public async Task OmittedNewStatus_Returns400AndLeavesLeadUnchanged()
    {
        var (factory, client, leadId) = await CreateHostWithLeadAsync();
        await using var _ = factory;

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"/api/v1/leads/{leadId}/status", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "an omitted newStatus must not silently bind to LeadStatus.Nuevo (member 0) and return 204");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lead = await db.Leads.FindAsync(leadId);
        lead!.Status.Should().Be(Domain.Leads.LeadStatus.Nuevo, "the lead's status must be unchanged by a rejected request");
    }

    [Theory]
    [InlineData("\"NotAStatus\"")]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("999")]
    public async Task InvalidNewStatusValues_Return400(string rawJsonValue)
    {
        var (factory, client, leadId) = await CreateHostWithLeadAsync();
        await using var _ = factory;

        using var content = new StringContent($$"""{"newStatus": {{rawJsonValue}} }""", Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"/api/v1/leads/{leadId}/status", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            $"newStatus={rawJsonValue} must be rejected with 400, not silently accepted");
    }

    [Fact]
    public async Task ExplicitNewStatus_StillUpdatesCorrectly()
    {
        var (factory, client, leadId) = await CreateHostWithLeadAsync();
        await using var _ = factory;

        using var content = new StringContent(
            """{"newStatus": "Contactado", "notes": "First contact made"}""", Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"/api/v1/leads/{leadId}/status", content);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lead = await db.Leads.FindAsync(leadId);
        lead!.Status.Should().Be(Domain.Leads.LeadStatus.Contactado);
    }

    /// <summary>An explicit "Nuevo" must still be honored — it is distinguishable from an omission.</summary>
    [Fact]
    public async Task ExplicitNuevo_IsDistinguishableFromOmission()
    {
        var (factory, client, leadId) = await CreateHostWithLeadAsync();
        await using var _ = factory;

        using var content = new StringContent("""{"newStatus": "Nuevo"}""", Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"/api/v1/leads/{leadId}/status", content);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "an explicit request for member 0 must be honored, unlike an omission");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lead = await db.Leads.FindAsync(leadId);
        lead!.Status.Should().Be(Domain.Leads.LeadStatus.Nuevo);
    }
}
