using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Platform.AuditLogs;
using Domain.DealerSettings.Events;
using Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Platform.AuditLogs;

public class RecordDealerLifecycleAuditHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handler_BuildsStableSourceEventKey_ForSuspendAndReactivate()
    {
        using var context = CreateContext();
        var dealer = new Domain.DealerSettings.DealerSettings(Guid.NewGuid(), "Dealer Inc", "dealer@test.com");
        context.DealerSettings.Add(dealer);
        await context.SaveChangesAsync();

        var recorder = new PlatformAuditLogRecorder(context);
        var handler = new RecordDealerLifecycleAuditHandler(recorder, context);

        var actorId = Guid.NewGuid();
        var suspendedAt = DateTime.UtcNow;
        var suspendEvent = new DealerSuspendedDomainEvent(dealer.Id, "Non-payment", actorId, suspendedAt);

        await handler.Handle(suspendEvent, CancellationToken.None);

        var log = await context.PlatformAuditLogs.SingleOrDefaultAsync();
        log.Should().NotBeNull();
        log!.SourceEventKey.Should().Be($"dealer-suspended:{dealer.Id:D}:{suspendedAt:O}");
        log.Action.Should().Be(PlatformAuditAction.DealerSuspended);
    }

    [Fact]
    public async Task Handler_BuildsStableSourceEventKey_ForReactivate()
    {
        using var context = CreateContext();
        var dealer = new Domain.DealerSettings.DealerSettings(Guid.NewGuid(), "Dealer Inc", "dealer@test.com");
        context.DealerSettings.Add(dealer);
        await context.SaveChangesAsync();

        var recorder = new PlatformAuditLogRecorder(context);
        var handler = new RecordDealerLifecycleAuditHandler(recorder, context);

        var actorId = Guid.NewGuid();
        var reactivatedAt = DateTime.UtcNow;
        var reactivateEvent = new DealerReactivatedDomainEvent(dealer.Id, actorId, reactivatedAt);

        await handler.Handle(reactivateEvent, CancellationToken.None);

        var log = await context.PlatformAuditLogs.SingleOrDefaultAsync();
        log.Should().NotBeNull();
        log!.SourceEventKey.Should().Be($"dealer-reactivated:{dealer.Id:D}:{reactivatedAt:O}");
        log.Action.Should().Be(PlatformAuditAction.DealerReactivated);
    }
}
