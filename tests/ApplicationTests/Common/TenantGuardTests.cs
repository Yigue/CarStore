using Application.Abstractions.Tenancy;
using Application.Common;
using FluentAssertions;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Common;

/// <summary>
/// Tests for <see cref="TenantGuard"/> — defense-in-handler tenant predicate.
/// REQ-FIN-TENANT-001 (enterprise-erp-crm/spec.md INVARIANT A amendment).
/// </summary>
public class TenantGuardTests
{
    private static ICurrentTenantService MakeTenant(bool hasTenant, Guid dealerId = default)
    {
        var mock = new Mock<ICurrentTenantService>();
        mock.SetupGet(t => t.HasTenant).Returns(hasTenant);
        mock.SetupGet(t => t.DealerId).Returns(dealerId);
        return mock.Object;
    }

    [Fact]
    public void EnsureHasTenant_NoTenant_ReturnsForbidden()
    {
        var tenant = MakeTenant(hasTenant: false);

        var result = TenantGuard.EnsureHasTenant(tenant);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void EnsureHasTenant_WithTenant_ReturnsSuccess()
    {
        var tenant = MakeTenant(hasTenant: true, dealerId: Guid.NewGuid());

        var result = TenantGuard.EnsureHasTenant(tenant);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void EnsureSameDealer_MatchingDealer_ReturnsSuccess()
    {
        var dealerId = Guid.NewGuid();
        var tenant = MakeTenant(hasTenant: true, dealerId: dealerId);

        var result = TenantGuard.EnsureSameDealer(tenant, dealerId);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void EnsureSameDealer_DifferentDealer_ReturnsForbidden()
    {
        var tenantDealer = Guid.NewGuid();
        var entityDealer = Guid.NewGuid();
        var tenant = MakeTenant(hasTenant: true, dealerId: tenantDealer);

        var result = TenantGuard.EnsureSameDealer(tenant, entityDealer);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void EnsureSameDealer_NoTenant_ReturnsForbidden()
    {
        var tenant = MakeTenant(hasTenant: false);

        var result = TenantGuard.EnsureSameDealer(tenant, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }
}