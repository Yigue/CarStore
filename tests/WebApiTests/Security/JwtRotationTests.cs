using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace WebApiTests.Security;

/// <summary>
/// spr PR 5.2 (B14): confirms the standard <c>ValidateIssuerSigningKey</c> JWT bearer guard
/// (already configured in <c>DependencyInjection.cs</c>) really does reject a token signed with a
/// different key — this is what makes rotating <c>Jwt:Secret</c> safe: every token issued under the
/// old key stops working the moment the new key is live, instead of being silently accepted.
/// </summary>
public sealed class JwtRotationTests
{
    [Fact]
    public async Task Me_WithTokenSignedByADifferentKey_Returns401()
    {
        await using var factory = new CustomWebApplicationFactory();
        factory.SeedDatabase();
        var client = factory.CreateClient();

        // The real admin token, issued under the key CustomWebApplicationFactory actually configures.
        string validToken = await IntegrationTestHelpers.GetAdminTokenAsync(factory);

        // A token with the exact same claims/issuer/audience as a real one, but signed with a
        // DIFFERENT key — simulates a token issued before a Jwt:Secret rotation.
        string oldKeyToken = CreateTokenWithKey("ADifferentSecretKeyNeverUsedByTheApp000000");

        using var httpClient = factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldKeyToken);

        HttpResponseMessage response = await httpClient.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "a token signed with a key other than the currently configured Jwt:Secret must never validate");

        // Sanity check: the real, currently-valid token for the same route still works — proves the
        // 401 above is about the signing key, not an unrelated problem with the route itself.
        IntegrationTestHelpers.SetAuthToken(client, validToken);
        HttpResponseMessage validResponse = await client.GetAsync("/api/v1/users/me");
        validResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string CreateTokenWithKey(string secretKey)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "admin@carstore.com"),
            new("dealer_id", Guid.NewGuid().ToString()),
            new("role", "Admin")
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            SigningCredentials = credentials,
            Issuer = "CarStore",
            Audience = "CarStore"
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }
}
