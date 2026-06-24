using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;

namespace WebApiTests.IntegrationTests.Rbac;

/// <summary>
/// RBAC backend hardening (spec: rbac). Verifies that the role/permission catalog,
/// dealer-settings mutation, and sensitive aggregate endpoints reject callers that
/// lack the required permission, while an authorized admin still succeeds.
/// </summary>
public class RbacEndpointTests
{
    private sealed record LoginResponse(string Token);
    private sealed record RegisterResponse(Guid Id);

    /// <summary>Registers a brand-new user (no permissions granted) and returns its bearer token.</summary>
    private static async Task<string> RegisterAndLoginFreshUserAsync(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"fresh_{Guid.NewGuid():N}@example.com";
        const string password = "Password1!";

        var register = new
        {
            Email = email,
            FirstName = "Fresh",
            LastName = "User",
            Password = password,
            DealerId = Guid.Parse(CustomWebApplicationFactory.AdminDealerId)
        };
        var registerResponse = await client.PostAsJsonAsync("/api/v1/users/register", register, IntegrationTestHelpers.JsonOptions);
        registerResponse.EnsureSuccessStatusCode();

        var login = new { Email = email, Password = password };
        var loginResponse = await client.PostAsJsonAsync("/api/v1/users/login", login, IntegrationTestHelpers.JsonOptions);
        loginResponse.EnsureSuccessStatusCode();
        var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        return result!.Token;
    }

    private static object ValidDealerSettingsPayload() => new
    {
        DealerName = "Lux Motors",
        ContactEmail = "contact@lux.com",
        NotificationsEnabled = true,
        HostName = (string?)null,
        CustomDomain = (string?)null,
        Address = "Av. Siempreviva 742",
        PhoneNumber = "+54 11 5555-5555",
        FacebookUrl = (string?)null,
        InstagramUrl = (string?)null,
        TwitterUrl = (string?)null,
        InterestRateTna = 0m
    };

    [Fact]
    public async Task GetRoles_WithoutAuth_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/roles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPermissions_WithoutAuth_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/permissions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRoles_WithUserLackingCanManageRoles_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync("/api/v1/roles");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetRoles_WithAdmin_Returns200()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync("/api/v1/roles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateDealerSettings_WithUserLackingCanManageSettings_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.PutAsJsonAsync("/api/v1/dealer-settings", ValidDealerSettingsPayload(), IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateDealerSettings_WithAdmin_Returns200()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await IntegrationTestHelpers.GetAdminTokenAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.PutAsJsonAsync("/api/v1/dealer-settings", ValidDealerSettingsPayload(), IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetFinancialSummary_WithUserLackingFinancialRead_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync("/api/v1/financial/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SearchClients_WithUserLackingClientsRead_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync("/api/v1/clients/search?q=foo");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
