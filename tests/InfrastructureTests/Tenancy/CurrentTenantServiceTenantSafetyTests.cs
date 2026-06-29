using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SharedKernel;

namespace InfrastructureTests.Tenancy;

/// <summary>
/// RED→GREEN tests for the critical <c>CurrentTenantService</c> tenant-safety fix
/// (openspec/changes/saas-custom-domains PR1 BE ADR-1).
///
/// Spec source:
/// <c>openspec/changes/saas-custom-domains/specs/tenant-safety-default-deny/spec.md</c>
///
/// Hard invariants under test:
///   1. Host miss → <c>Guid.Empty</c>. NEVER a first-row fallback.
///   2. Dev convenience path (<c>Tenant:DevFallbackDealerId</c>) only fires
///      when <c>IHostEnvironment.IsDevelopment()</c>.
///   3. In Staging/Production, host miss → <c>Guid.Empty</c> (+ Critical log in prod).
///   4. Empty host header → <c>Guid.Empty</c>.
/// </summary>
public class CurrentTenantServiceTenantSafetyTests : IDisposable
{
    private const string RealDealerId = "11111111-1111-1111-1111-111111111111";
    private const string HostageDealerId = "22222222-2222-2222-2222-222222222222";

    private readonly SqliteConnection _connection;

    public CurrentTenantServiceTenantSafetyTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    /// <summary>
    /// Builds a fresh <see cref="ApplicationDbContext"/> on the in-memory Sqlite
    /// connection and seeds two dealers, returning the resolved service-provider
    /// that exposes the context (so the SUT's <c>RequestServices.GetService(IApplicationDbContext)</c>
    /// resolves correctly).
    /// </summary>
    private IServiceProvider BuildSeededContextProvider()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddScoped<ApplicationDbContext>(sp =>
            new ApplicationDbContext(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>(),
                new NoOpPublisher(),
                new NoOpTenantService()));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();

            if (!context.DealerSettings.IgnoreQueryFilters().Any())
            {
                context.DealerSettings.AddRange(
                    new Domain.DealerSettings.DealerSettings(
                        Guid.Parse(RealDealerId),
                        "Real Dealer",
                        "real@example.com",
                        slug: "real-dealer",
                        hostName: "xyz.carstore.com"),
                    new Domain.DealerSettings.DealerSettings(
                        Guid.Parse(HostageDealerId),
                        "Hostage Dealer",
                        "hostage@example.com",
                        slug: "hostage-dealer",
                        hostName: "other.carstore.com"));
                context.SaveChanges();
            }
        }

        return provider;
    }

    private static Mock<IHttpContextAccessor> BuildAccessor(string? host = null, string? xTenantHost = null, IServiceProvider? services = null)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        if (host is not null)
        {
            ctx.Request.Headers.Host = host;
        }

        if (xTenantHost is not null)
        {
            ctx.Request.Headers["X-Tenant-Host"] = xTenantHost;
        }

        if (services is not null)
        {
            ctx.RequestServices = services;
        }

        accessor.Setup(a => a.HttpContext).Returns(ctx);
        return accessor;
    }

    private static CurrentTenantService NewService(
        IHttpContextAccessor accessor,
        IHostEnvironment env,
        Guid? devFallbackDealerId = null)
    {
        var options = Options.Create(new TenantFallbackOptions(devFallbackDealerId));
        return new CurrentTenantService(
            accessor,
            env,
            options,
            NullLogger<CurrentTenantService>.Instance);
    }

    private static IHostEnvironment DevEnv() => new StubHostEnv(Environments.Development);
    private static IHostEnvironment ProdEnv() => new StubHostEnv(Environments.Production);
    private static IHostEnvironment StagingEnv() => new StubHostEnv(Environments.Staging);

    // ── 1: Host miss must NEVER leak the first-row dealer ─────────────────────

    [Fact]
    public void DealerId_HostMiss_ReturnsGuidEmpty_NotFirstRowFallback()
    {
        var services = BuildSeededContextProvider();
        var accessor = BuildAccessor(host: "unknown.carstore.com", services: services);

        var sut = NewService(accessor.Object, DevEnv());

        var result = sut.DealerId;

        result.Should().Be(Guid.Empty,
            "a host that matches no DealerSettings row MUST return Guid.Empty — " +
            "returning the first row would leak a different tenant's data.");
        result.Should().NotBe(Guid.Parse(HostageDealerId),
            "the hostage dealer's Id must NEVER leak via first-row fallback");
    }

    [Fact]
    public void DealerId_HostMiss_DevFallbackDealerIdConfigured_StillReturnsGuidEmpty_WhenOutsideDevelopment()
    {
        var services = BuildSeededContextProvider();
        var accessor = BuildAccessor(host: "unknown.carstore.com", services: services);

        var sut = NewService(accessor.Object, ProdEnv(), devFallbackDealerId: Guid.Parse(RealDealerId));

        sut.DealerId.Should().Be(Guid.Empty,
            "Tenant:DevFallbackDealerId MUST be ignored in Production.");
    }

    // ── 2: Host hit resolves to the correct dealer ────────────────────────────

    [Fact]
    public void DealerId_KnownHost_ReturnsThatDealersId_CaseInsensitive()
    {
        var services = BuildSeededContextProvider();
        var accessor = BuildAccessor(host: "XYZ.carstore.com", services: services);

        var sut = NewService(accessor.Object, ProdEnv());

        sut.DealerId.Should().Be(Guid.Parse(RealDealerId),
            "host match wins regardless of the configured dev fallback");
    }

    // ── 3: Dev fallback fires ONLY when env is Development ────────────────────

    [Fact]
    public void DealerId_HostMiss_WithDevFallbackConfigured_InDevelopment_UsesFallback()
    {
        var services = BuildSeededContextProvider();
        var accessor = BuildAccessor(host: "localhost:3000", services: services);

        var sut = NewService(accessor.Object, DevEnv(), devFallbackDealerId: Guid.Parse(RealDealerId));

        sut.DealerId.Should().Be(Guid.Parse(RealDealerId),
            "in Development, the convenience fallback MUST be honored");
    }

    [Fact]
    public void DealerId_HostMiss_WithDevFallbackConfigured_InProduction_ReturnsEmpty()
    {
        var services = BuildSeededContextProvider();
        var accessor = BuildAccessor(host: "127.0.0.1:5000", services: services);

        var sut = NewService(accessor.Object, ProdEnv(), devFallbackDealerId: Guid.Parse(RealDealerId));

        sut.DealerId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void DealerId_HostMiss_WithDevFallbackConfigured_InStaging_ReturnsEmpty()
    {
        var services = BuildSeededContextProvider();
        var accessor = BuildAccessor(host: "127.0.0.1:5000", services: services);

        var sut = NewService(accessor.Object, StagingEnv(), devFallbackDealerId: Guid.Parse(RealDealerId));

        sut.DealerId.Should().Be(Guid.Empty);
    }

    // ── 4: Empty headers cannot fabricate a tenant ───────────────────────────

    [Fact]
    public void DealerId_NoHeaders_NoJwt_NoFallback_ReturnsGuidEmpty()
    {
        var services = BuildSeededContextProvider();
        var accessor = BuildAccessor(services: services);

        var sut = NewService(accessor.Object, ProdEnv());

        sut.DealerId.Should().Be(Guid.Empty);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

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

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class NoOpTenantService : ICurrentTenantService
    {
        public Guid DealerId => Guid.Empty;
        public bool HasTenant => false;
    }
}
