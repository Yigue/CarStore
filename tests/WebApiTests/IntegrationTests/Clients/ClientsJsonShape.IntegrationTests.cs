using Domain.Clients;
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
                DateTime.UtcNow);

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

        // camelCase keys MUST be present
        json.TryGetProperty("id", out _).Should().BeTrue("key 'id' (camelCase) must be present");
        json.TryGetProperty("dni", out _).Should().BeTrue("key 'dni' (lowercase) must be present");
        json.TryGetProperty("address", out _).Should().BeTrue("key 'address' (camelCase) must be present");
        json.TryGetProperty("firstName", out _).Should().BeTrue("key 'firstName' (camelCase) must be present");

        // PascalCase keys MUST NOT be present
        json.TryGetProperty("Id", out _).Should().BeFalse("PascalCase key 'Id' must NOT be present");
        json.TryGetProperty("DNI", out _).Should().BeFalse("PascalCase key 'DNI' must NOT be present");
        json.TryGetProperty("Address", out _).Should().BeFalse("PascalCase key 'Address' must NOT be present");
        json.TryGetProperty("FirstName", out _).Should().BeFalse("PascalCase key 'FirstName' must NOT be present");

        // Values should match what was seeded
        json.TryGetProperty("firstName", out var firstNameProp).Should().BeTrue();
        firstNameProp.GetString().Should().Be("Jane");

        json.TryGetProperty("dni", out var dniProp).Should().BeTrue();
        dniProp.GetString().Should().Be("30000001");

        json.TryGetProperty("address", out var addressProp).Should().BeTrue();
        addressProp.GetString().Should().Be("Av. Corrientes 1234");
    }
}
