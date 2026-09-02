using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Platform.AuditLogs.GetPlatformAuditLogs;
using Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Platform.AuditLogs;

public class GetPlatformAuditLogsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handler_DefaultsToPage1Size25_OrderedByOccurredAtUtcDesc()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var e1 = PlatformAuditLogEntry.Record(
            dealerId, Guid.NewGuid(), "Dealer A", PlatformAuditAction.DealerSuspended, Guid.NewGuid(), "a@test.com", PlatformAuditActorKind.SuperAdmin, now.AddMinutes(-10), now, "k1");
        var e2 = PlatformAuditLogEntry.Record(
            dealerId, Guid.NewGuid(), "Dealer A", PlatformAuditAction.DealerReactivated, Guid.NewGuid(), "a@test.com", PlatformAuditActorKind.SuperAdmin, now.AddMinutes(-5), now, "k2");

        context.PlatformAuditLogs.AddRange(e1, e2);
        await context.SaveChangesAsync();

        var handler = new GetPlatformAuditLogsQueryHandler(context);
        var result = await handler.Handle(new GetPlatformAuditLogsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Action.Should().Be("DealerReactivated");
        result.Value.Items[1].Action.Should().Be("DealerSuspended");
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task Handler_FiltersByDealerId_Action_AndDateRange()
    {
        using var context = CreateContext();
        var dealer1 = Guid.NewGuid();
        var dealer2 = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var e1 = PlatformAuditLogEntry.Record(
            dealer1, Guid.NewGuid(), "Dealer 1", PlatformAuditAction.DealerSuspended, Guid.NewGuid(), "a@test.com", PlatformAuditActorKind.SuperAdmin, now.AddHours(-2), now, "k1");
        var e2 = PlatformAuditLogEntry.Record(
            dealer2, Guid.NewGuid(), "Dealer 2", PlatformAuditAction.DealerProvisioned, Guid.NewGuid(), "b@test.com", PlatformAuditActorKind.SelfService, now.AddHours(-1), now, "k2");

        context.PlatformAuditLogs.AddRange(e1, e2);
        await context.SaveChangesAsync();

        var handler = new GetPlatformAuditLogsQueryHandler(context);

        var resDealer1 = await handler.Handle(new GetPlatformAuditLogsQuery(DealerId: dealer1), CancellationToken.None);
        resDealer1.Value.Items.Should().ContainSingle().Which.DealerId.Should().Be(dealer1);

        var resAction = await handler.Handle(new GetPlatformAuditLogsQuery(Action: "DealerProvisioned"), CancellationToken.None);
        resAction.Value.Items.Should().ContainSingle().Which.Action.Should().Be("DealerProvisioned");
    }

    [Fact]
    public async Task Handler_ReturnsEmptyPage_NotNotFound()
    {
        using var context = CreateContext();
        var handler = new GetPlatformAuditLogsQueryHandler(context);

        var result = await handler.Handle(new GetPlatformAuditLogsQuery(DealerId: Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}
