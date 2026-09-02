using Domain.Leads;
using Domain.Leads.Events;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using WebApiTests.IntegrationTests;

namespace WebApiTests.IntegrationTests.Clients;

/// <summary>
/// PR1 integration gates (task 1.4.6):
/// 1. ConvertLead accepts an explicit Type field; Corporate persists correctly.
/// 2. Exactly ONE LeadStatusChangedDomainEvent with NewStatus=Ganado is written to
///    the outbox per convert call — guards against duplicate event emission.
/// </summary>
public class ConvertLeadAcceptsTypeIntegrationTests
{
    private static readonly JsonSerializerOptions CaseSensitiveOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<Guid> SeedLeadAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var lead = Lead.Create(dealerId, "Convert TypeTest", "type.test@example.com", "9998887", LeadSource.Web, DateTime.UtcNow);
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        return lead.Id;
    }

    [Fact]
    public async Task ConvertLead_WithCorporateType_CreatesClientWithCorrectType()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var leadId = await SeedLeadAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        // Act: send Type=Corporate in convert request
        var payload = new { Dni = "88776655", Address = "Calle Corporativa 1", Type = "Corporate" };
        var response = await httpClient.PostAsJsonAsync($"/api/v1/leads/{leadId}/convert", payload, IntegrationTestHelpers.JsonOptions);

        // Assert: 201
        response.StatusCode.Should().Be(HttpStatusCode.Created, "convert with Corporate type must return 201 Created");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        body.TryGetProperty("clientId", out var clientIdProp).Should().BeTrue("response must contain clientId");
        var clientId = clientIdProp.GetGuid();

        // Verify persisted type via GET /clients/{id}
        var clientResponse = await httpClient.GetAsync($"/api/v1/clients/{clientId}");
        clientResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var clientJson = await clientResponse.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);

        clientJson.TryGetProperty("type", out var typeProp).Should().BeTrue("key 'type' must be present");
        typeProp.GetString().Should().Be("Corporate", "the persisted ClientType must echo back as 'Corporate'");
    }

    [Fact]
    public async Task ConvertLead_EmitsNoGanadoEvent_InOutbox()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        var leadId = await SeedLeadAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        var payload = new { Dni = "77665544", Address = "Ev. Unica 1", Type = "Individual" };

        // Act
        var response = await httpClient.PostAsJsonAsync($"/api/v1/leads/{leadId}/convert", payload, IntegrationTestHelpers.JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert: NO LeadStatusChangedDomainEvent with NewStatus=Ganado for this lead.
        //
        // This test used to demand exactly one, from back when converting a lead also closed the
        // deal. REQ-2.3 removed that path on purpose — registering someone as a client says
        // nothing about whether they bought — and ArchitectureTests.SinglePathToWonTests now
        // fails the build if any handler other than the three sale-driven ones writes Ganado.
        // The assertion was left behind and had been red ever since; inverting it is what makes
        // it agree with the rule the codebase actually enforces.
        //
        // Scoped by AggregateId==leadId: unscoped, this would also count the Ganado lead
        // DevDataSeeder creates (a legitimate fixture) in the same in-memory DB.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // OutboxMessage.Content is serialized with Newtonsoft.Json (TypeNameHandling.None),
        // which serializes enums as integers by default — so NewStatus is 4, not "Ganado".
        const int GanadoOrdinal = (int)LeadStatus.Ganado;
        var ganadoEvents = db.OutboxMessages
            .AsNoTracking()
            .AsEnumerable()
            .Where(m => m.AggregateId == leadId)
            .Where(m =>
            {
                if (!m.Type.Equals(nameof(LeadStatusChangedDomainEvent), StringComparison.Ordinal))
                    return false;
                try
                {
                    var eventData = JsonSerializer.Deserialize<JsonElement>(m.Content);
                    if (!eventData.TryGetProperty("NewStatus", out var ns)) return false;
                    // Supports both numeric (Newtonsoft default) and string (future JsonStringEnumConverter)
                    return (ns.ValueKind == JsonValueKind.Number && ns.GetInt32() == GanadoOrdinal)
                        || (ns.ValueKind == JsonValueKind.String && ns.GetString() == nameof(LeadStatus.Ganado));
                }
                catch
                {
                    return false;
                }
            })
            .ToList();

        ganadoEvents.Should().BeEmpty(
            "converting a lead into a client must not close the deal — Ganado follows a registered sale (REQ-2.3)");
    }
}
