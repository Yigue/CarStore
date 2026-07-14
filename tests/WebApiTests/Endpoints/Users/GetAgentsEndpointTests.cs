using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace WebApiTests.Endpoints.Users;

/// <summary>
/// Covers GET /api/v1/users/agents: a lightweight staff directory gated by
/// sales:read (not CanManageUsers), so non-admin staff can populate the
/// "Vendedor" select on SaleForm without hitting a 403.
/// </summary>
public class GetAgentsEndpointTests
{
    [Fact]
    public async Task GetAgents_ReturnsUnauthorized_WithoutToken()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/users/agents");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAgents_ReturnsForbidden_WithoutSalesReadPermission()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        var register = new
        {
            Email = "noaccess@example.com",
            FirstName = "No",
            LastName = "Access",
            Password = "Password1!",
            DealerId = System.Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        await client.PostAsJsonAsync("/api/v1/users/register", register);

        var login = new { Email = "noaccess@example.com", Password = "Password1!" };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        IntegrationTestHelpers.SetAuthToken(client, loginResult!.token);

        var response = await client.GetAsync("/api/v1/users/agents");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAgents_ReturnsActiveStaff_WhenAuthorizedWithSalesRead()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        // The seeded admin has sales:read among its full permission set.
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync("/api/v1/users/agents");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var agents = await response.Content.ReadFromJsonAsync<List<AgentResponseDto>>(IntegrationTestHelpers.JsonOptions);

        agents.Should().NotBeNull();
        // Seed data includes admin@carstore.com (Admin) and empleado@carstore.com (Empleado).
        agents!.Select(a => a.Role).Should().Contain(new[] { "Admin", "Empleado" });
        agents.Should().OnlyContain(a => a.Role == "Admin" || a.Role == "Empleado");
        agents.Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.FullName));
    }

    private sealed record LoginResponse(string token);

    private sealed record AgentResponseDto(
        System.Guid Id,
        string FirstName,
        string LastName,
        string FullName,
        string Role);
}
