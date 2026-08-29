using Domain.Leads;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Leads;

public class LeadsJsonShapeIntegrationTests
{
    [Fact]
    public async Task GetLeads_Returns_Status_And_Source_As_String()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Leads.Add(Domain.Leads.Lead.Create(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Test Client",
                "test@lead.com",
                "123456",
                Domain.Leads.LeadSource.Web,
                DateTime.UtcNow
            ));
            await db.SaveChangesAsync();
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        // Act
        var response = await client.GetAsync("/api/v1/leads");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var jsonArray = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        jsonArray.ValueKind.Should().Be(JsonValueKind.Array);
        jsonArray.GetArrayLength().Should().BeGreaterThan(0);

        var firstLead = jsonArray[0];

        firstLead.TryGetProperty("status", out var statusValue).Should().BeTrue("Property 'status' should exist in the JSON response");
        statusValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'status' should be a string (enum converted)");
        statusValue.GetString().Should().NotBeNullOrEmpty();

        firstLead.TryGetProperty("source", out var sourceValue).Should().BeTrue("Property 'source' should exist in the JSON response");
        sourceValue.ValueKind.Should().Be(JsonValueKind.String, "Property 'source' should be a string (enum converted)");
        sourceValue.GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// POST /leads/{id}/convert → 201, the client is created with camelCase keys (id, dni,
    /// address) and correct persisted values, and the lead's stage is left where it was.
    ///
    /// <para>
    /// REQ-2.3 supersedes this test's original expectation that conversion advance the lead to
    /// Ganado. Registering someone as a client is not the same fact as closing a deal, and the
    /// old rule let this endpoint report won deals with no sale behind them. The stage assertion
    /// below is the old one inverted, on purpose.
    /// </para>
    /// </summary>
    [Fact]
    public async Task ConvertLead_LeavesTheStageAlone_AndCreatesClientWithCamelCaseShape()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        Guid leadId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
            var lead = Lead.Create(dealerId, "Convert Test", "convert.test@example.com", "3334445", LeadSource.Web, DateTime.UtcNow);
            db.Leads.Add(lead);
            await db.SaveChangesAsync();
            leadId = lead.Id;
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        var convertRequest = new { Dni = "12345678", Address = "Av. Rivadavia 1001" };

        // Act: POST /api/v1/leads/{id}/convert
        var convertResponse = await httpClient.PostAsJsonAsync($"/api/v1/leads/{leadId}/convert", convertRequest);

        // Assert 201
        convertResponse.StatusCode.Should().Be(HttpStatusCode.Created, "convert must return 201 Created");

        // Extract clientId from response
        var convertJson = await convertResponse.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        var clientId = convertJson.GetProperty("clientId").GetGuid();
        clientId.Should().NotBeEmpty("clientId must be a valid Guid");

        // Verify lead status via DB
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var updatedLead = await db.Leads.FindAsync(leadId);
            updatedLead!.Status.Should().Be(
                LeadStatus.Nuevo,
                "conversion creates the client record and nothing else; only a sale moves the lead to Ganado");
        }

        // Verify client JSON shape: GET /api/v1/clients/{clientId}
        var clientResponse = await httpClient.GetAsync($"/api/v1/clients/{clientId}");
        clientResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Case-SENSITIVE assertion (default JsonSerializerOptions, no PropertyNameCaseInsensitive)
        var clientJson = await clientResponse.Content.ReadFromJsonAsync<JsonElement>();

        clientJson.TryGetProperty("id", out _).Should().BeTrue("key 'id' (camelCase) must be present");
        // PR1 (task 1.2.2): field renamed from 'dni' → 'documentNumber' in ClientResponse DTO
        clientJson.TryGetProperty("documentNumber", out var dniProp).Should().BeTrue("key 'documentNumber' (camelCase) must be present");
        clientJson.TryGetProperty("address", out var addrProp).Should().BeTrue("key 'address' (camelCase) must be present");

        dniProp.GetString().Should().Be("12345678", "persisted document number must match the convert request Dni");
        addrProp.GetString().Should().Be("Av. Rivadavia 1001", "persisted address must match the convert request");
    }
}
