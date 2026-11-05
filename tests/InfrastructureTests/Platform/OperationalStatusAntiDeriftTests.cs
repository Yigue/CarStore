using System;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Application.Abstractions.Tenancy;
using Domain.Billing;
using Domain.DealerSettings;
using FluentAssertions;
using Infrastructure.Database;
using Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace InfrastructureTests.Platform;

/// <summary>
/// spr ADR B7 (PR 2.1, task 2.1.3): anti-drift matrix. <see cref="DealerOperationalStatusResolver.Resolve"/>
/// is a pure projection used by the super-admin screen; it must never disagree with what the two REAL
/// enforcement middlewares (<see cref="DealerSuspensionMiddleware"/> for manual suspension,
/// <see cref="SubscriptionGuardMiddleware"/> for billing) actually do to a tenant request for the same
/// <c>IsActive</c> x billing-standing combination. If someone changes one side without the other, one of
/// these cases must fail.
///
/// Mapping asserted here (today's production wiring, not <see cref="DealerOperationalStatusResolver.DecideAccess"/>'s
/// 7-day PastDue grace, which is a documented pending business decision — see spr PR 3.1/D2 — and is not
/// yet consumed by any middleware):
/// <list type="bullet">
/// <item><see cref="DealerOperationalStatus.SuspendedManual"/> -&gt; 403 (DealerSuspensionMiddleware blocks, SuperAdmin aside).</item>
/// <item><see cref="DealerOperationalStatus.Active"/> -&gt; both middlewares call next() (pass-through).</item>
/// <item>Any other category (<c>BillingPending</c>/<c>PastDue</c>/<c>Suspended</c>/<c>Cancelled</c>) -&gt; 402 (SubscriptionGuardMiddleware blocks).</item>
/// </list>
/// </summary>
public sealed class OperationalStatusAntiDeriftTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;

    public OperationalStatusAntiDeriftTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockPublisher = new Mock<MediatR.IPublisher>();
        var mockTenant = new Mock<ICurrentTenantService>();
        mockTenant.Setup(t => t.HasTenant).Returns(false);

        _db = new ApplicationDbContext(options, mockPublisher.Object, mockTenant.Object);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    public static readonly TheoryData<bool, SubscriptionStatus?, DealerOperationalStatus> Matrix = new()
    {
        { false, null, DealerOperationalStatus.SuspendedManual },
        { false, SubscriptionStatus.Active, DealerOperationalStatus.SuspendedManual },
        { true, null, DealerOperationalStatus.BillingPending },
        { true, SubscriptionStatus.Incomplete, DealerOperationalStatus.BillingPending },
        { true, SubscriptionStatus.Active, DealerOperationalStatus.Active },
        { true, SubscriptionStatus.Trialing, DealerOperationalStatus.Active },
        { true, SubscriptionStatus.PastDue, DealerOperationalStatus.PastDue },
        { true, SubscriptionStatus.Suspended, DealerOperationalStatus.Suspended },
        { true, SubscriptionStatus.Cancelled, DealerOperationalStatus.Cancelled },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Resolve_MatchesTheRealMiddlewares_ForEveryIsActiveXStandingCombination(
        bool isActive,
        SubscriptionStatus? status,
        DealerOperationalStatus expectedCategory)
    {
        // The pure projection used by the super-admin screen.
        var actualCategory = DealerOperationalStatusResolver.Resolve(isActive, BillingStandingMapper.From(status));
        actualCategory.Should().Be(expectedCategory, "the test matrix itself must match the resolver's precedence");

        var dealerId = Guid.NewGuid();
        var settings = new DealerSettings(dealerId, "AntiDrift Dealer", "antidrift@dealer.test");
        if (!isActive)
        {
            settings.Suspend("anti-drift test", Guid.NewGuid(), DateTime.UtcNow);
        }
        _db.DealerSettings.Add(settings);
        await _db.SaveChangesAsync();

        // --- Gate 1: DealerSuspensionMiddleware (real class, real DB-backed IsActive read) ---
        var suspensionNextCalled = false;
        var suspensionMiddleware = new DealerSuspensionMiddleware(
            _ => { suspensionNextCalled = true; return Task.CompletedTask; },
            NullLogger<DealerSuspensionMiddleware>.Instance);

        var tenantServiceMock = new Mock<ICurrentTenantService>();
        tenantServiceMock.Setup(t => t.HasTenant).Returns(true);
        tenantServiceMock.Setup(t => t.DealerId).Returns(dealerId);

        var suspensionServices = new ServiceCollection();
        suspensionServices.AddSingleton<Application.Abstractions.Data.IApplicationDbContext>(_db);
        suspensionServices.AddSingleton(tenantServiceMock.Object);

        var suspensionContext = new DefaultHttpContext
        {
            RequestServices = suspensionServices.BuildServiceProvider(),
        };
        suspensionContext.Request.Path = "/api/v1/clients";
        suspensionContext.Request.Method = "GET";

        await suspensionMiddleware.InvokeAsync(suspensionContext);

        if (expectedCategory == DealerOperationalStatus.SuspendedManual)
        {
            suspensionNextCalled.Should().BeFalse("manual suspension must block before billing is ever evaluated");
            suspensionContext.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            return; // manual suspension outranks billing — the resolver agrees, no need to probe the billing gate.
        }

        suspensionNextCalled.Should().BeTrue("an IsActive dealer must pass the manual-suspension gate");

        // --- Gate 2: SubscriptionGuardMiddleware (real class, mocked cache mirroring the production Redis cache) ---
        var guardNextCalled = false;
        var guardMiddleware = new SubscriptionGuardMiddleware(
            _ => { guardNextCalled = true; return Task.CompletedTask; });

        var cacheMock = new Mock<ISubscriptionStatusCache>();
        cacheMock.Setup(c => c.GetAsync(dealerId, default)).ReturnsAsync(status);

        var guardServices = new ServiceCollection();
        guardServices.AddSingleton(cacheMock.Object);

        var guardContext = new DefaultHttpContext
        {
            RequestServices = guardServices.BuildServiceProvider(),
        };
        guardContext.Items["Tenant.DealerId"] = dealerId;
        guardContext.Request.Path = "/api/v1/clients";
        guardContext.Request.Method = "GET";

        await guardMiddleware.InvokeAsync(guardContext);

        if (expectedCategory == DealerOperationalStatus.Active)
        {
            guardNextCalled.Should().BeTrue("Active/Trialing billing standing must pass the subscription gate");
        }
        else
        {
            guardNextCalled.Should().BeFalse($"{expectedCategory} must be blocked by the subscription gate");
            guardContext.Response.StatusCode.Should().Be(StatusCodes.Status402PaymentRequired);
        }
    }
}
