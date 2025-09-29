using System.Security.Claims;
using Infrastructure.Authentication;

namespace AuthenticationTests;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_ReturnsGuid_WhenClaimIsValid()
    {
        Guid userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }));

        Guid result = principal.GetUserId();

        result.Should().Be(userId);
    }

    [Fact]
    public void GetUserId_Throws_WhenPrincipalIsNull()
    {
        ClaimsPrincipal? principal = null;

        Action act = () => principal.GetUserId();

        act.Should().Throw<ApplicationException>();
    }

    [Fact]
    public void GetUserId_Throws_WhenClaimIsInvalid()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid")
        }));

        Action act = () => principal.GetUserId();

        act.Should().Throw<ApplicationException>();
    }
}
