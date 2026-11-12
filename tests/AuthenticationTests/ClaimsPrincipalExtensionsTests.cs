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

    [Fact]
    public void IsPlatformSuperAdmin_ReturnsTrue_WhenPlatformRoleIsSuperAdminAndDealerIdIsEmpty()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("platform_role", "super_admin"),
            new Claim("dealer_id", Guid.Empty.ToString())
        }));

        principal.IsPlatformSuperAdmin().Should().BeTrue();
    }

    [Fact]
    public void IsPlatformSuperAdmin_ReturnsTrue_WhenPlatformRoleIsSuperAdminAndDealerIdIsAbsent()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("platform_role", "super_admin")
        }));

        principal.IsPlatformSuperAdmin().Should().BeTrue();
    }

    [Fact]
    public void IsPlatformSuperAdmin_ReturnsFalse_WhenPrincipalIsNull()
    {
        ClaimsPrincipal? principal = null;

        principal.IsPlatformSuperAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsPlatformSuperAdmin_ReturnsFalse_WhenPlatformRoleIsMissing()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("role", "Admin"),
            new Claim("dealer_id", Guid.Empty.ToString())
        }));

        principal.IsPlatformSuperAdmin().Should().BeFalse();
    }

    [Fact]
    public void IsPlatformSuperAdmin_ReturnsFalse_WhenPlatformRoleIsSuperAdminButDealerIdIsNotEmpty()
    {
        var tenantDealerId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("platform_role", "super_admin"),
            new Claim("dealer_id", tenantDealerId.ToString())
        }));

        principal.IsPlatformSuperAdmin().Should().BeFalse(
            "A token with both platform_role and a tenant dealer_id must NOT be treated as a platform superadmin");
    }
}
