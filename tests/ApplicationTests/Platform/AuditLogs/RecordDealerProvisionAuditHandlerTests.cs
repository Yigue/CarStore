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

public class RecordDealerProvisionAuditHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task ProvisionHandler_UsesEventEmail_AndSelfServiceKind()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var dealer = new Domain.DealerSettings.DealerSettings(
            id: dealerId,
            dealerId: dealerId,
            dealerName: "New Dealer",
            contactEmail: "admin@newdealer.com");
        context.DealerSettings.Add(dealer);
        await context.SaveChangesAsync();

        var recorder = new PlatformAuditLogRecorder(context);
        var handler = new RecordDealerProvisionAuditHandler(recorder, context);

        var adminUserId = Guid.NewGuid();
        var provisionEvent = new DealerProvisionedDomainEvent(
            DealerId: dealerId,
            AdminUserId: adminUserId,
            AdminEmail: "admin@newdealer.com",
            Subdomain: "newdealer",
            DashboardUrl: "https://newdealer.carstore.com");

        await handler.Handle(provisionEvent, CancellationToken.None);

        var log = await context.PlatformAuditLogs.SingleOrDefaultAsync();
        log.Should().NotBeNull();
        log!.SourceEventKey.Should().Be($"dealer-provisioned:{dealerId:D}");
        log.Action.Should().Be(PlatformAuditAction.DealerProvisioned);
        log.ActorKind.Should().Be(PlatformAuditActorKind.SelfService);
        log.ActorUserId.Should().Be(adminUserId);
        log.ActorEmail.Should().Be("admin@newdealer.com");
    }
}
