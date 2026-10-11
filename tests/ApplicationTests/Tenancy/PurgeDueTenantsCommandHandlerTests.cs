using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Tenancy.Purge;
using Domain.DealerSettings;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;
using Xunit;

namespace Application.UnitTests.Tenancy;

/// <summary>
/// RN-013 (A1.4): the daily run locks tenants whose 30 day grace ended, then purges ONLY the Locked ones that are due,
/// one dealer per command. A dealer that fails does not stop the others.
/// </summary>
public class PurgeDueTenantsCommandHandlerTests
{
    private static readonly DateTime Cancelled = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = Cancelled.AddDays(91);

    private readonly TestApplicationDbContext _context = new(
        new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<ITenantLifecycleCache> _cache = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly List<PurgeTenantCommand> _sent = [];

    public PurgeDueTenantsCommandHandlerTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _sender
            .Setup(s => s.Send(It.IsAny<IRequest<Result<TenantPurgeResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IRequest<Result<TenantPurgeResult>> r, CancellationToken _) =>
            {
                var c = (PurgeTenantCommand)r;
                _sent.Add(c);
                return Result.Success(new TenantPurgeResult(c.DryRun, new Dictionary<string, int>()));
            });
    }

    private PurgeDueTenantsCommandHandler Handler(Dictionary<string, string?>? config = null) =>
        new(_context, _sender.Object, _clock.Object, _cache.Object,
            new ConfigurationBuilder().AddInMemoryCollection(config ?? new Dictionary<string, string?>()).Build(),
            NullLogger<PurgeDueTenantsCommandHandler>.Instance);

    private Guid Seed(TenantLifecycleStatus status, DateTime? cancelledAt = null)
    {
        var id = Guid.NewGuid();
        var dealer = new Domain.DealerSettings.DealerSettings(id, "Dealer", $"{id:N}@d.com");
        if (status != TenantLifecycleStatus.Active)
        {
            dealer.StartRetention(cancelledAt ?? Cancelled, 90);
            if (status == TenantLifecycleStatus.Locked)
                dealer.LockAfterGrace((cancelledAt ?? Cancelled).AddDays(31), 30);
        }

        _context.DealerSettings.Add(dealer);
        _context.SaveChanges();
        return id;
    }

    private async Task<TenantLifecycleStatus> StatusOf(Guid id) =>
        (await _context.DealerSettings.SingleAsync(d => d.DealerId == id)).LifecycleStatus;

    [Fact]
    public async Task LocksGraceTenantsPastThirtyDays_AndLeavesTheRestAlone()
    {
        var pastGrace = Seed(TenantLifecycleStatus.CancelledGrace, Now.AddDays(-31));
        var inGrace = Seed(TenantLifecycleStatus.CancelledGrace, Now.AddDays(-5));
        var active = Seed(TenantLifecycleStatus.Active);

        var result = await Handler().Handle(new PurgeDueTenantsCommand(DryRun: true), CancellationToken.None);

        (await StatusOf(pastGrace)).Should().Be(TenantLifecycleStatus.Locked);
        (await StatusOf(inGrace)).Should().Be(TenantLifecycleStatus.CancelledGrace);
        (await StatusOf(active)).Should().Be(TenantLifecycleStatus.Active);
        result.Value.Locked.Should().Be(1);
        _cache.Verify(c => c.Invalidate(pastGrace), Times.Once);
        _cache.Verify(c => c.Invalidate(inGrace), Times.Never);
        _cache.Verify(c => c.Invalidate(active), Times.Never);
    }

    [Fact]
    public async Task OnlyDueLockedTenants_ReceiveAPurgeCommand()
    {
        var due = Seed(TenantLifecycleStatus.Locked, Now.AddDays(-91));
        var lockedNotDue = Seed(TenantLifecycleStatus.Locked, Now.AddDays(-45));
        var grace = Seed(TenantLifecycleStatus.CancelledGrace, Now.AddDays(-5));
        var active = Seed(TenantLifecycleStatus.Active);

        await Handler().Handle(new PurgeDueTenantsCommand(DryRun: false), CancellationToken.None);

        _sent.Select(c => c.DealerId).Should().BeEquivalentTo(new[] { due });
        _sent.Should().OnlyContain(c => !c.DryRun);
        _sent.Select(c => c.DealerId).Should().NotContain(new[] { lockedNotDue, grace, active });
    }

    [Fact]
    public async Task OneCommandPerDealer_NeverAGlobalOne()
    {
        var a = Seed(TenantLifecycleStatus.Locked, Now.AddDays(-100));
        var b = Seed(TenantLifecycleStatus.Locked, Now.AddDays(-95));

        await Handler().Handle(new PurgeDueTenantsCommand(DryRun: false), CancellationToken.None);

        _sent.Should().HaveCount(2);
        _sent.Select(c => c.DealerId).Should().BeEquivalentTo(new[] { a, b });
        _sent.Should().OnlyContain(c => c.DealerId != Guid.Empty);
    }

    [Fact]
    public async Task DryRunFlag_IsPassedThrough()
    {
        Seed(TenantLifecycleStatus.Locked, Now.AddDays(-100));

        await Handler().Handle(new PurgeDueTenantsCommand(DryRun: true), CancellationToken.None);

        _sent.Should().ContainSingle().Which.DryRun.Should().BeTrue();
    }

    [Fact]
    public async Task AFailingDealer_DoesNotStopTheOthers_AndIsCounted()
    {
        var bad = Seed(TenantLifecycleStatus.Locked, Now.AddDays(-100));
        var good = Seed(TenantLifecycleStatus.Locked, Now.AddDays(-99));
        _sender
            .Setup(s => s.Send(It.Is<IRequest<Result<TenantPurgeResult>>>(c => ((PurgeTenantCommand)c).DealerId == bad), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await Handler().Handle(new PurgeDueTenantsCommand(DryRun: false), CancellationToken.None);

        result.Value.Failed.Should().Be(1);
        result.Value.Purged.Should().Be(1);
        _sent.Select(c => c.DealerId).Should().Contain(good);
    }

    [Fact]
    public async Task DeferredOrRejectedPurges_AreCountedAsSkipped()
    {
        var id = Seed(TenantLifecycleStatus.Locked, Now.AddDays(-100));
        _sender
            .Setup(s => s.Send(It.IsAny<IRequest<Result<TenantPurgeResult>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<TenantPurgeResult>(TenantPurgeErrors.ExportStillAvailable));

        var result = await Handler().Handle(new PurgeDueTenantsCommand(DryRun: false), CancellationToken.None);

        result.Value.Skipped.Should().Be(1);
        result.Value.Purged.Should().Be(0);
        id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HonoursConfiguredGraceDays()
    {
        var id = Seed(TenantLifecycleStatus.CancelledGrace, Now.AddDays(-10));

        await Handler(new() { ["Retention:GraceDays"] = "7" }).Handle(new PurgeDueTenantsCommand(DryRun: true), CancellationToken.None);

        (await StatusOf(id)).Should().Be(TenantLifecycleStatus.Locked);
    }
}
