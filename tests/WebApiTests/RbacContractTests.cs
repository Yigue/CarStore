using System.Text.Json;

namespace WebApiTests;

/// <summary>
/// CFG-01: the RBAC read endpoints answer with a named envelope, not a bare array.
///
/// <para>
/// <c>GET /api/v1/roles</c> used to unwrap <c>RolesResponse</c> and return the array directly,
/// which matched neither the response type the endpoint declared nor what the dashboard reads
/// (<c>rbacService.getRoles</c> takes <c>response.roles</c>). Reading <c>.roles</c> off an array
/// yields <c>undefined</c>, the user form had no role id to post, and <c>POST /api/v1/users</c>
/// answered 400 for every role in the list. These tests pin the shape so the contract cannot
/// drift back.
/// </para>
/// </summary>
public class RbacContractTests
{
    [Fact]
    public async Task GetRoles_ReturnsAnEnvelopeWithARolesArray()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, await IntegrationTestHelpers.GetAdminTokenAsync(factory));

        var response = await client.GetAsync("/api/v1/roles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.ValueKind.Should().Be(
            JsonValueKind.Object,
            "the endpoint declares RolesResponse — a bare array is a third shape nobody declared");
        payload.RootElement.TryGetProperty("roles", out var roles).Should().BeTrue();
        roles.ValueKind.Should().Be(JsonValueKind.Array);
    }

    // Every role id has to be a GUID: CreateUserCommandHandler parses it as one, and the
    // dashboard posts back exactly what this endpoint handed it.
    [Fact]
    public async Task GetRoles_ReturnsRoleIdsTheUserEndpointsCanParse()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, await IntegrationTestHelpers.GetAdminTokenAsync(factory));

        var response = await client.GetAsync("/api/v1/roles");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        foreach (var role in payload.RootElement.GetProperty("roles").EnumerateArray())
        {
            Guid.TryParse(role.GetProperty("id").GetString(), out _).Should().BeTrue();
            role.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task GetPermissions_ReturnsAnEnvelopeWithAPermissionsArray()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, await IntegrationTestHelpers.GetAdminTokenAsync(factory));

        var response = await client.GetAsync("/api/v1/permissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        payload.RootElement.TryGetProperty("permissions", out var permissions).Should().BeTrue();
        permissions.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
