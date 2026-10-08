using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Domain.Users;
using Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;

namespace InfrastructureTests.Authentication;

public class TokenProviderTests
{
    [Fact]
    public void Create_ReturnsValidToken()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "supersecretkeysupersecretkeysupersecretkey",
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:ExpirationInMinutes"] = "60"
        }).Build();

        var provider = new TokenProvider(configuration);
                var user = new User(Guid.Parse("11111111-1111-1111-1111-111111111111"), "test@example.com", "Test", "User", "hash", Guid.NewGuid());

        string token = provider.Create(user, "empleado");

        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.First(c => c.Type == Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Email).Value.Should().Be(user.Email);
        jwt.Claims.First(c => c.Type == "role").Value.Should().Be("empleado");
        jwt.Claims.First(c => c.Type == "dealer_id").Value.Should().Be(user.DealerId.ToString());
        jwt.Issuer.Should().Be("issuer");
        jwt.Audiences.Should().Contain("audience");
        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Create_Throws_WhenSecretMissing()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:ExpirationInMinutes"] = "60"
        }).Build();

        var provider = new TokenProvider(configuration);
                var user = new User(Guid.Parse("11111111-1111-1111-1111-111111111111"), "test@example.com", "Test", "User", "hash", Guid.NewGuid());

        Action act = () => provider.Create(user, "empleado");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_DoesNotEmitPlatformRoleClaim_WhenUserHasNonEmptyDealerIdEvenIfRoleIdIsEmpty()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "supersecretkeysupersecretkeysupersecretkey",
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:ExpirationInMinutes"] = "60"
        }).Build();

        var provider = new TokenProvider(configuration);
        var dealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        // User with DealerId and empty RoleId (e.g. legacy or orphan user)
        var user = new User(dealerId, "tenant@example.com", "Tenant", "User", "hash", Guid.Empty);

        string token = provider.Create(user, "Cliente");

        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        jwt.Claims.Any(c => c.Type == "platform_role").Should().BeFalse(
            "Users belonging to a dealer must NEVER receive the platform_role claim, even with empty RoleId");
    }

    [Fact]
    public void Create_EmitsPlatformRoleClaim_WhenUserIsSuperAdmin()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "supersecretkeysupersecretkeysupersecretkey",
            ["Jwt:Issuer"] = "issuer",
            ["Jwt:Audience"] = "audience",
            ["Jwt:ExpirationInMinutes"] = "60"
        }).Build();

        var provider = new TokenProvider(configuration);
        var superAdmin = User.CreateSuperAdmin("superadmin@carstore.com", "Super", "Admin", "hash");

        string token = provider.Create(superAdmin, "SuperAdmin");

        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        jwt.Claims.First(c => c.Type == "platform_role").Value.Should().Be("super_admin");
    }
}
