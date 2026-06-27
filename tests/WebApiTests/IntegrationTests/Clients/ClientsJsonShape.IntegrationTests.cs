using Domain.Clients;
using Domain.Clients.Attributes;
using Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace WebApiTests.IntegrationTests.Clients;

/// <summary>
/// Characterization tests that lock the JSON shape of the Clients HTTP contract.
/// Uses case-SENSITIVE TryGetProperty to ensure literal key names — NOT the
/// case-insensitive JsonOptions in IntegrationTestHelpers.
/// </summary>
public class ClientsJsonShapeIntegrationTests
{
    [Fact]
    public async Task GetClientById_Returns_CamelCase_Keys()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();

        Guid clientId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            var dealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId);
            var entity = new Client(
                dealerId,
                "Jane",
                "Smith",
                "30000001",
                "jane.shape@example.com",
                "1234567890",
                "Av. Corrientes 1234",
                DateTime.UtcNow,
                ClientType.Corporate);

            db.Clients.Add(entity);
            await db.SaveChangesAsync();
            clientId = entity.Id;
        }

        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        // Act
        var response = await httpClient.GetAsync($"/api/v1/clients/{clientId}");

        // Assert: HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Use default JsonSerializerOptions (case-SENSITIVE) to assert literal key names
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // camelCase keys MUST be present (PR1 data-contract expansion)
        json.TryGetProperty("id", out _).Should().BeTrue("key 'id' (camelCase) must be present");
        json.TryGetProperty("documentNumber", out _).Should().BeTrue("key 'documentNumber' must be present (was 'dni' before PR1)");
        json.TryGetProperty("address", out _).Should().BeTrue("key 'address' (camelCase) must be present");
        json.TryGetProperty("firstName", out _).Should().BeTrue("key 'firstName' (camelCase) must be present");
        json.TryGetProperty("lastName", out _).Should().BeTrue("key 'lastName' (camelCase) must be present");
        json.TryGetProperty("fullName", out _).Should().BeTrue("key 'fullName' (camelCase) must be present (PR1 new field)");
        json.TryGetProperty("tenantId", out _).Should().BeTrue("key 'tenantId' must be present (PR1 new field)");
        json.TryGetProperty("type", out _).Should().BeTrue("key 'type' (camelCase) must be present");
        json.TryGetProperty("status", out _).Should().BeTrue("key 'status' (camelCase) must be present");

        // PascalCase keys MUST NOT be present
        json.TryGetProperty("Id", out _).Should().BeFalse("PascalCase key 'Id' must NOT be present");
        json.TryGetProperty("DNI", out _).Should().BeFalse("PascalCase key 'DNI' must NOT be present");
        json.TryGetProperty("Address", out _).Should().BeFalse("PascalCase key 'Address' must NOT be present");
        json.TryGetProperty("FirstName", out _).Should().BeFalse("PascalCase key 'FirstName' must NOT be present");

        // Values should match what was seeded
        json.TryGetProperty("firstName", out var firstNameProp).Should().BeTrue();
        firstNameProp.GetString().Should().Be("Jane");

        json.TryGetProperty("documentNumber", out var dniProp).Should().BeTrue();
        dniProp.GetString().Should().Be("30000001");

        json.TryGetProperty("address", out var addressProp).Should().BeTrue();
        addressProp.GetString().Should().Be("Av. Corrientes 1234");

        // PR1: enums must travel as PascalCase strings, NOT integers.
        json.TryGetProperty("type", out var typeProp).Should().BeTrue();
        typeProp.ValueKind.Should().Be(JsonValueKind.String, "ClientType must serialize as PascalCase string");
        typeProp.GetString().Should().Be("Corporate");

        json.TryGetProperty("status", out var statusProp).Should().BeTrue();
        statusProp.ValueKind.Should().Be(JsonValueKind.String, "ClientStatus must serialize as PascalCase string");
    }

    [Fact]
    public async Task CreateClient_RejectsInvalidType_With400()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        // type=99 is not a valid ClientType — JsonStringEnumConverter will reject it.
        var invalidPayload = new
        {
            firstName = "OutOfRange",
            lastName = "Type",
            email = "oor.type@example.com",
            phone = "1234567",
            address = "Calle 123",
            dni = "99000001",
            type = 99
        };

        var response = await httpClient.PostAsJsonAsync("/api/v1/clients", invalidPayload, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "an out-of-range enum value must be rejected");
    }

    [Fact]
    public async Task CreateClient_AcceptsCorporateType_EndToEnd()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        var payload = new
        {
            firstName = "ACME",
            lastName = "S.A.",
            email = $"acme.e2e.{Guid.NewGuid():N}@example.com",
            phone = "1234567",
            address = "Calle Industrial 1",
            dni = $"33{Guid.NewGuid().ToString("N")[..6]}",
            type = "Corporate"
        };

        var response = await httpClient.PostAsJsonAsync("/api/v1/clients", payload, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created, "a string enum value 'Corporate' must round-trip cleanly");

        // Create returns { id } — fetch the full DTO to verify the persisted type.
        var rawBody = await response.Content.ReadAsStringAsync();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);
        body.TryGetProperty("id", out var idProp).Should().BeTrue($"response body was: {rawBody}");
        var createdId = idProp.GetString();
        createdId.Should().NotBeNullOrEmpty();

        var fetched = await httpClient.GetAsync($"/api/v1/clients/{createdId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedBody = await fetched.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestHelpers.JsonOptions);

        fetchedBody.TryGetProperty("type", out var typeProp).Should().BeTrue();
        typeProp.GetString().Should().Be("Corporate", "the persisted type must echo back as the PascalCase string 'Corporate'");
    }

    /// <summary>
    /// REQ-004 / enterprise-erp-crm / crm-client-data-contract:
    /// A request body with an unknown field must be rejected with HTTP 400.
    /// This guards against silent field-name drift (e.g. a client renaming
    /// 'type' to 'clientType' and never getting an error).
    /// </summary>
    [Fact]
    public async Task CreateClient_WithUnknownField_Returns400()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var httpClient = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(httpClient, token);

        // Valid required fields PLUS one unknown field that the endpoint's
        // Request record does not declare → must be rejected at deserialization.
        var payload = new
        {
            firstName = "Jane",
            lastName = "Doe",
            email = $"req004.{Guid.NewGuid():N}@example.com",
            phone = "1234567",
            address = "Av. Test 123",
            dni = $"40{Guid.NewGuid().ToString("N")[..6]}",
            type = "Individual",
            favoriteColor = "blue"   // unknown field — must cause 400
        };

        var response = await httpClient.PostAsJsonAsync("/api/v1/clients", payload, IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "REQ-004: a request body containing an unknown field must be rejected with 400, not silently succeed");
    }
}
