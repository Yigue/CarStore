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
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com" };

        string token = provider.Create(user);

        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(token);

        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be(user.Email);
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
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com" };

        Action act = () => provider.Create(user);

        act.Should().Throw<NullReferenceException>();
    }
}
