using Infrastructure.Tenancy;
using Application.Abstractions.Tenancy;
using Application.Abstractions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using FluentAssertions;

namespace InfrastructureTests.Tenancy;

/// <summary>
/// Smoke tests for the renamed/widened <see cref="CurrentTenantService"/> constructor
/// (PR1 saas-custom-domains ADR-1). The behavioral assertions around host miss
/// and dev-fallback gating live in <c>CurrentTenantServiceTenantSafetyTests</c>.
/// </summary>
public class CurrentTenantServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IOptions<TenantFallbackOptions> _fallbackOptions;
    private readonly CurrentTenantService _sut;

    public CurrentTenantServiceTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        // The "Test" environment is treated like Production: dev fallback MUST be
        // off, host miss MUST return Guid.Empty. (See TenantSafetyTests for the matrix.)
        _hostEnvironment = new StubHostEnv("Test");

        _fallbackOptions = Options.Create(new TenantFallbackOptions());

        _sut = new CurrentTenantService(
            _httpContextAccessorMock.Object,
            _hostEnvironment,
            _fallbackOptions,
            NullLogger<CurrentTenantService>.Instance);
    }

    private sealed class StubHostEnv : IHostEnvironment
    {
        public StubHostEnv(string name)
        {
            EnvironmentName = name;
            ApplicationName = "InfrastructureTests";
            ContentRootPath = AppContext.BaseDirectory;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public bool IsDevelopment() => string.Equals(EnvironmentName, Environments.Development, StringComparison.OrdinalIgnoreCase);
        public bool IsProduction() => string.Equals(EnvironmentName, Environments.Production, StringComparison.OrdinalIgnoreCase);
        public bool IsStaging() => string.Equals(EnvironmentName, Environments.Staging, StringComparison.OrdinalIgnoreCase);
        public bool IsEnvironment(string environmentName) => string.Equals(EnvironmentName, environmentName, StringComparison.OrdinalIgnoreCase);
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
