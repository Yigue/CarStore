using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using WebApiTests.Postgres;
using Xunit;

namespace WebApiTests.Security;

[Collection("PostgresCollection")]
[Trait("Category", "Postgres")]
public class RegisterDoesNotGrantPlatformRoleTests : IAsyncLifetime
{
    private readonly PostgresWebApplicationFactory _factory;

    public RegisterDoesNotGrantPlatformRoleTests(PostgresFixture fixture)
    {
        _factory = new PostgresWebApplicationFactory(fixture.GetConnectionString());
    }

    public async Task InitializeAsync() => await _factory.InitializeDatabaseAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private sealed record LoginResponse(string Token);

    [Fact]
    public async Task RegisteredUser_TokenMustNotContain_PlatformRoleClaim()
    {
        var client = _factory.CreateClient();
        var uniqueEmail = $"tenant_user_{Guid.NewGuid():N}@example.com";
        var password = "SecurePassword123!";

        // 1. Register new user
        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/users/register",
            new
            {
                Email = uniqueEmail,
                FirstName = "Test",
                LastName = "User",
                Password = password
            },
            IntegrationTestHelpers.JsonOptions);

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Login as the newly registered user
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/users/login",
            new
            {
                Email = uniqueEmail,
                Password = password
            },
            IntegrationTestHelpers.JsonOptions);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(IntegrationTestHelpers.JsonOptions);
        loginResult.Should().NotBeNull();
        loginResult!.Token.Should().NotBeNullOrWhiteSpace();

        // 3. Inspect JWT token
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(loginResult.Token);

        // Assert: platform_role claim must not exist
        jwt.Claims.Should().NotContain(c => c.Type == "platform_role",
            because: "regular registered users must never be granted the platform_role claim");

        // Assert: role claim must not be SuperAdmin
        jwt.Claims.Should().NotContain(c => c.Type == "role" && c.Value.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase),
            because: "regular registered users must not be given the SuperAdmin role");
    }
}
