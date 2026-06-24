using Infrastructure.Tenancy;
using Application.Abstractions.Tenancy;
using Application.Abstractions.Data;
using Microsoft.AspNetCore.Http;
using Moq;
using FluentAssertions;

namespace InfrastructureTests.Tenancy;

public class CurrentTenantServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly CurrentTenantService _sut;

    public CurrentTenantServiceTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _sut = new CurrentTenantService(_httpContextAccessorMock.Object);
    }

    [Fact]
    public void DealerId_WhenJwtDealerIdClaim_ReturnsDealerIdFromClaim()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var claims = new List<System.Security.Claims.Claim>
        {
            new("dealer_id", dealerId.ToString())
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        _httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        // Act
        var result = _sut.DealerId;

        // Assert
        result.Should().Be(dealerId);
    }

    [Fact]
    public void HasTenant_WhenJwtDealerIdClaim_ReturnsTrue()
    {
        // Arrange
        var dealerId = Guid.NewGuid();
        var claims = new List<System.Security.Claims.Claim>
        {
            new("dealer_id", dealerId.ToString())
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };

        _httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        // Act
        var result = _sut.HasTenant;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void DealerId_WhenNoJwtClaimAndNoTenantHost_ReturnsEmptyGuid()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        // Act
        var result = _sut.DealerId;

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void HasTenant_WhenNoJwtClaimAndNoTenant_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        _httpContextAccessorMock
            .Setup(x => x.HttpContext)
            .Returns(httpContext);

        // Act
        var result = _sut.HasTenant;

        // Assert
        result.Should().BeFalse();
    }
}