using Domain.Clients;
using Domain.Shared;
using Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApiTests.IntegrationTests.Clients;

/// <summary>
/// PR2 integration tests for PUT /clients/{id}/notes and GET /clients/{id}/activity.
/// </summary>
public class NotesAndActivityEndpointTests
{
    private static async Task<Guid> SeedClientAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
        var client = new Client(dealerId, "Notes", "Test", "50000001", "notes.test@example.com", "555", "Av. Test 1", DateTime.UtcNow);
        // Clear domain events so the seed itself does not pollute the outbox for activity tests.
        client.ClearDomainEvents();
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    // ─── UpdateNotes Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateNotes_Returns200_WithClientResponse()
    {
        await using var factory = new CustomWebApplicationFactory();
        var clientId = await SeedClientAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);

        var payload = new { Notes = "Integration test notes" };
        var response = await http.PutAsJsonAsync($"/api/v1/clients/{clientId}/notes", payload, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "PUT /clients/{id}/notes should return 200 with updated ClientResponse");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        body.TryGetProperty("id", out var idProp).Should().BeTrue("ClientResponse must include 'id'");
        idProp.GetGuid().Should().Be(clientId);
        body.TryGetProperty("notes", out var notesProp).Should().BeTrue("ClientResponse must include 'notes'");
        notesProp.GetString().Should().Be("Integration test notes");
    }

    [Fact]
    public async Task UpdateNotes_Returns404_WhenClientNotFound()
    {
        await using var factory = new CustomWebApplicationFactory();

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);

        var payload = new { Notes = "Should not matter" };
        var response = await http.PutAsJsonAsync($"/api/v1/clients/{Guid.NewGuid()}/notes", payload, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateNotes_Returns422_WhenNotesTooLong()
    {
        await using var factory = new CustomWebApplicationFactory();
        var clientId = await SeedClientAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);

        var payload = new { Notes = new string('x', 2001) };
        var response = await http.PutAsJsonAsync($"/api/v1/clients/{clientId}/notes", payload, IntegrationTestHelpers.JsonOptions);

        // FluentValidation pipeline returns 422 or 400 for validation errors
        ((int)response.StatusCode).Should().BeOneOf(400, 422);
    }

    [Fact]
    public async Task UpdateNotes_AllowsNullNotes_ToClearField()
    {
        await using var factory = new CustomWebApplicationFactory();
        var clientId = await SeedClientAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);

        // First set notes
        await http.PutAsJsonAsync($"/api/v1/clients/{clientId}/notes", new { Notes = "Initial" }, IntegrationTestHelpers.JsonOptions);

        // Then clear them
        var payload = new { Notes = (string?)null };
        var response = await http.PutAsJsonAsync($"/api/v1/clients/{clientId}/notes", payload, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        body.TryGetProperty("notes", out var notesProp).Should().BeTrue();
        notesProp.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task UpdateNotes_WritesOutboxMessage_WithAggregateId()
    {
        await using var factory = new CustomWebApplicationFactory();
        var clientId = await SeedClientAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);

        var payload = new { Notes = "Outbox test" };
        var response = await http.PutAsJsonAsync($"/api/v1/clients/{clientId}/notes", payload, IntegrationTestHelpers.JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var outboxMsg = db.OutboxMessages
            .AsEnumerable()
            .FirstOrDefault(m => m.AggregateId == clientId && m.AggregateType == "Client");

        outboxMsg.Should().NotBeNull("UpdateNotes must produce an OutboxMessage with AggregateId and AggregateType populated");
        outboxMsg!.AggregateId.Should().Be(clientId);
        outboxMsg.AggregateType.Should().Be("Client");
        outboxMsg.DealerId.Should().NotBeNull();
    }

    // ─── GetActivity Tests ────────────────────────────────────────────────────

    [Fact]
    public async Task GetActivity_Returns200_WithEmptyItems_WhenNoEvents()
    {
        await using var factory = new CustomWebApplicationFactory();
        var clientId = await SeedClientAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);

        var response = await http.GetAsync($"/api/v1/clients/{clientId}/activity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        body.TryGetProperty("items", out var items).Should().BeTrue();
        items.GetArrayLength().Should().Be(0);
        body.TryGetProperty("totalCount", out var total).Should().BeTrue();
        total.GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task GetActivity_Returns_Events_After_UpdateNotes()
    {
        await using var factory = new CustomWebApplicationFactory();
        var clientId = await SeedClientAsync(factory);

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);

        // Trigger an event
        await http.PutAsJsonAsync($"/api/v1/clients/{clientId}/notes", new { Notes = "Activity source" }, IntegrationTestHelpers.JsonOptions);

        var response = await http.GetAsync($"/api/v1/clients/{clientId}/activity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        body.TryGetProperty("totalCount", out var total).Should().BeTrue();
        total.GetInt32().Should().Be(1);
        body.TryGetProperty("items", out var items).Should().BeTrue();
        items.GetArrayLength().Should().Be(1);

        var first = items[0];
        first.TryGetProperty("eventType", out var et).Should().BeTrue();
        et.GetString().Should().Be("ClientNotesUpdatedDomainEvent");
    }

    [Fact]
    public async Task GetActivity_Returns404_WhenClientBelongsToAnotherTenant()
    {
        await using var factory = new CustomWebApplicationFactory();

        // Seed a client owned by a DIFFERENT dealer (another tenant). During seeding
        // there is no active tenant, so the explicit DealerId is persisted as-is.
        Guid otherTenantClientId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
            var otherDealerId = Guid.NewGuid();
            var client = new Client(otherDealerId, "Other", "Tenant", "99999999", "other.tenant@example.com", "555", "Av. Other 1", DateTime.UtcNow);
            client.ClearDomainEvents();
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            otherTenantClientId = client.Id;
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var http = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(http, token);

        var response = await http.GetAsync($"/api/v1/clients/{otherTenantClientId}/activity");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "activity for a client owned by another tenant must not leak — cross-tenant access returns 404");
    }
}
