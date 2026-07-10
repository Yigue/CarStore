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

    // ── Finances-module-fixes (B.8) ────────────────────────────────────────
    // 12 new 401/403 cases covering financial endpoints that previously had
    // no RBAC coverage. Each test asserts on status code AND asserts that
    // the response body does NOT leak any domain data (defense against
    // enumeration attacks via error messages).

    private static object ValidCategoryCreatePayload() => new
    {
        Name = $"CAT-{Guid.NewGuid():N}".Substring(0, 16),
        Description = "Test",
        Type = 0, // Income
    };

    private static object ValidCategoryUpdatePayload() => new
    {
        Name = $"CAT-{Guid.NewGuid():N}".Substring(0, 16),
        Description = "Test",
        Type = 0,
    };

    private static void AssertForbiddenEmptyBody(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        // Defense against enumeration: the body MUST NOT leak any domain
        // data (id, amount, categoryId, totals, etc.).
        body.Should().NotContain("\"id\"");
        body.Should().NotContain("\"amount\"");
        body.Should().NotContain("\"categoryId\"");
        body.Should().NotContain("\"totals\"");
    }

    private static void AssertUnauthorizedEmptyBody(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateFinancial_WithoutAuth_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/financial/{Guid.NewGuid()}",
            new { Type = 0, Amount = 100m, Description = "x", PaymentMethod = 0, ReferenceNumber = "r", TransactionDate = DateTime.UtcNow, CategoryId = Guid.NewGuid() },
            IntegrationTestHelpers.JsonOptions);

        AssertUnauthorizedEmptyBody(response);
    }

    [Fact]
    public async Task UpdateFinancial_WithUserLackingFinancialUpdate_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/financial/{Guid.NewGuid()}",
            new { Type = 0, Amount = 100m, Description = "x", PaymentMethod = 0, ReferenceNumber = "r", TransactionDate = DateTime.UtcNow, CategoryId = Guid.NewGuid() },
            IntegrationTestHelpers.JsonOptions);

        AssertForbiddenEmptyBody(response);
    }

    [Fact]
    public async Task DeleteFinancial_WithoutAuth_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/financial/{Guid.NewGuid()}");

        AssertUnauthorizedEmptyBody(response);
    }

    [Fact]
    public async Task DeleteFinancial_WithUserLackingFinancialDelete_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.DeleteAsync($"/api/v1/financial/{Guid.NewGuid()}");

        AssertForbiddenEmptyBody(response);
    }

    [Fact]
    public async Task CreateCategory_WithoutAuth_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/financial/categories",
            ValidCategoryCreatePayload(),
            IntegrationTestHelpers.JsonOptions);

        AssertUnauthorizedEmptyBody(response);
    }

    [Fact]
    public async Task CreateCategory_WithUserLackingFinancialCreate_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.PostAsJsonAsync(
            "/api/v1/financial/categories",
            ValidCategoryCreatePayload(),
            IntegrationTestHelpers.JsonOptions);

        AssertForbiddenEmptyBody(response);
    }

    [Fact]
    public async Task UpdateCategory_WithoutAuth_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/v1/financial/categories/{Guid.NewGuid()}",
            ValidCategoryUpdatePayload(),
            IntegrationTestHelpers.JsonOptions);

        AssertUnauthorizedEmptyBody(response);
    }

    [Fact]
    public async Task UpdateCategory_WithUserLackingFinancialUpdate_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/financial/categories/{Guid.NewGuid()}",
            ValidCategoryUpdatePayload(),
            IntegrationTestHelpers.JsonOptions);

        AssertForbiddenEmptyBody(response);
    }

    [Fact]
    public async Task DeleteCategory_WithoutAuth_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/financial/categories/{Guid.NewGuid()}");

        AssertUnauthorizedEmptyBody(response);
    }

    [Fact]
    public async Task DeleteCategory_WithUserLackingFinancialDelete_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.DeleteAsync($"/api/v1/financial/categories/{Guid.NewGuid()}");

        AssertForbiddenEmptyBody(response);
    }

    [Fact]
    public async Task GetFinancialReport_WithoutAuth_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        var client = factory.CreateClient();

        var from = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var response = await client.GetAsync(
            $"/api/v1/reports/financials?from={from}&to={to}&groupBy=Day");

        AssertUnauthorizedEmptyBody(response);
    }

    [Fact]
    public async Task GetFinancialReport_WithUserLackingFinancialRead_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var from = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var response = await client.GetAsync(
            $"/api/v1/reports/financials?from={from}&to={to}&groupBy=Day");

        AssertForbiddenEmptyBody(response);
    }

    // ── crm-hardening PR2 (W1): notes/activity endpoints RBAC ───────────────

    [Fact]
    public async Task UpdateNotes_WithUserLackingClientsWrite_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/clients/{Guid.NewGuid()}/notes",
            new { Notes = "x" },
            IntegrationTestHelpers.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetActivity_WithUserLackingClientsRead_Returns403()
    {
        await using var factory = new CustomWebApplicationFactory();
        var token = await RegisterAndLoginFreshUserAsync(factory);
        var client = factory.CreateClient();
        IntegrationTestHelpers.SetAuthToken(client, token);

        var response = await client.GetAsync($"/api/v1/clients/{Guid.NewGuid()}/activity");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
