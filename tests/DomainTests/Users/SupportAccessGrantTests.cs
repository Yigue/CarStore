using Domain.Users;
using FluentAssertions;
using SharedKernel;
using Xunit;

namespace DomainTests.Users;

/// <summary>
/// sfc A4.3.5 / A19 / RN-032: Dealer authorizes read-only support access for N hours with audit trail.
/// </summary>
public class SupportAccessGrantTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Grant_WithValidData_SetsPropertiesAndIsActive()
    {
        var grant = SupportAccessGrant.Grant(
            DealerId,
            AdminUserId,
            supportIdentifier: "support-agent-42@platform.carstore.com",
            durationHours: 8,
            reason: "Investigating lead notification issue",
            nowUtc: Now);

        grant.Id.Should().NotBeEmpty();
        grant.DealerId.Should().Be(DealerId);
        grant.GrantedByUserId.Should().Be(AdminUserId);
        grant.SupportIdentifier.Should().Be("support-agent-42@platform.carstore.com");
        grant.DurationHours.Should().Be(8);
        grant.Reason.Should().Be("Investigating lead notification issue");
        grant.GrantedAtUtc.Should().Be(Now);
        grant.ExpiresAtUtc.Should().Be(Now.AddHours(8));
        grant.RevokedAtUtc.Should().BeNull();
        grant.RevokedByUserId.Should().BeNull();
        grant.IsActive(Now).Should().BeTrue();
        grant.IsActive(Now.AddHours(4)).Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(73)]
    public void Grant_WithInvalidDuration_ThrowsDomainException(int invalidHours)
    {
        Action act = () => SupportAccessGrant.Grant(
            DealerId,
            AdminUserId,
            "support@carstore.com",
            invalidHours,
            "Assisting customer",
            Now);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Grant_WithEmptySupportIdentifier_ThrowsDomainException(string? invalidIdentifier)
    {
        Action act = () => SupportAccessGrant.Grant(
            DealerId,
            AdminUserId,
            invalidIdentifier!,
            4,
            "Assisting customer",
            Now);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Grant_WithEmptyReason_ThrowsDomainException(string? invalidReason)
    {
        Action act = () => SupportAccessGrant.Grant(
            DealerId,
            AdminUserId,
            "support@carstore.com",
            4,
            invalidReason!,
            Now);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenExpired()
    {
        var grant = SupportAccessGrant.Grant(
            DealerId,
            AdminUserId,
            "support@carstore.com",
            2,
            "Quick check",
            Now);

        grant.IsActive(Now.AddHours(1)).Should().BeTrue();
        grant.IsActive(Now.AddHours(2)).Should().BeFalse();
        grant.IsActive(Now.AddHours(3)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_SetsRevokedPropertiesAndDeactivates()
    {
        var grant = SupportAccessGrant.Grant(
            DealerId,
            AdminUserId,
            "support@carstore.com",
            12,
            "Needs investigation",
            Now);

        var revokeTime = Now.AddHours(1);
        grant.Revoke(AdminUserId, revokeTime);

        grant.RevokedAtUtc.Should().Be(revokeTime);
        grant.RevokedByUserId.Should().Be(AdminUserId);
        grant.IsActive(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsDomainException()
    {
        var grant = SupportAccessGrant.Grant(
            DealerId,
            AdminUserId,
            "support@carstore.com",
            12,
            "Needs investigation",
            Now);

        grant.Revoke(AdminUserId, Now.AddHours(1));

        Action act = () => grant.Revoke(AdminUserId, Now.AddHours(2));
        act.Should().Throw<DomainException>();
    }
}
