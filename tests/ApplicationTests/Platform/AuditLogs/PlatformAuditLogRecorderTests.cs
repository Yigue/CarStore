using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Platform.AuditLogs;
using Domain.DealerSettings;
using Domain.Platform;
using Domain.Users;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Platform.AuditLogs;

public class PlatformAuditLogRecorderTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Recorder_WritesEntry_SnapshottingActorEmailAndDealerName()
    {
        using var context = CreateContext();
        var dealer = new Domain.DealerSettings.DealerSettings(Guid.NewGuid(), "Dealer Inc", "dealer@test.com");
        context.DealerSettings.Add(dealer);

        var actorUser = User.CreateSuperAdmin("admin@carstore.com", "Super", "Admin", "hashed");
        context.Users.Add(actorUser);
        await context.SaveChangesAsync();

        var recorder = new PlatformAuditLogRecorder(context);
        var now = DateTime.UtcNow;

        var entry = await recorder.RecordAsync(
            dealerSettingsId: dealer.Id,
            action: PlatformAuditAction.DealerSuspended,
            actorUserId: actorUser.Id,
            actorKind: PlatformAuditActorKind.SuperAdmin,
            occurredAtUtc: now,
            sourceEventKey: "key-1",
            reason: "Non-payment",
            cancellationToken: CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.DealerId.Should().Be(dealer.DealerId);
        entry.DealerSettingsId.Should().Be(dealer.Id);
        entry.DealerName.Should().Be("Dealer Inc");
        entry.ActorUserId.Should().Be(actorUser.Id);
        entry.ActorEmail.Should().Be("admin@carstore.com");
        entry.Reason.Should().Be("Non-payment");
    }

    [Fact]
    public async Task Recorder_WhenActorUserNoLongerExists_WritesNullEmail()
    {
        using var context = CreateContext();
        var dealer = new Domain.DealerSettings.DealerSettings(Guid.NewGuid(), "Dealer Inc", "dealer@test.com");
        context.DealerSettings.Add(dealer);
        await context.SaveChangesAsync();

        var recorder = new PlatformAuditLogRecorder(context);
        var missingActorId = Guid.NewGuid();

        var entry = await recorder.RecordAsync(
            dealerSettingsId: dealer.Id,
            action: PlatformAuditAction.DealerSuspended,
            actorUserId: missingActorId,
            actorKind: PlatformAuditActorKind.SuperAdmin,
            occurredAtUtc: DateTime.UtcNow,
            sourceEventKey: "key-2",
            cancellationToken: CancellationToken.None);

        entry.Should().NotBeNull();
        entry!.ActorEmail.Should().BeNull();
    }

    [Fact]
    public async Task Recorder_OnRedelivery_SkipsBySourceEventKey()
    {
        using var context = CreateContext();
        var dealer = new Domain.DealerSettings.DealerSettings(Guid.NewGuid(), "Dealer Inc", "dealer@test.com");
        context.DealerSettings.Add(dealer);
        await context.SaveChangesAsync();

        var recorder = new PlatformAuditLogRecorder(context);
        var first = await recorder.RecordAsync(
            dealerSettingsId: dealer.Id,
            action: PlatformAuditAction.DealerSuspended,
            actorUserId: Guid.NewGuid(),
            actorKind: PlatformAuditActorKind.SuperAdmin,
            occurredAtUtc: DateTime.UtcNow,
            sourceEventKey: "key-duplicate",
            cancellationToken: CancellationToken.None);
        await context.SaveChangesAsync();

        var second = await recorder.RecordAsync(
            dealerSettingsId: dealer.Id,
            action: PlatformAuditAction.DealerSuspended,
            actorUserId: Guid.NewGuid(),
            actorKind: PlatformAuditActorKind.SuperAdmin,
            occurredAtUtc: DateTime.UtcNow,
            sourceEventKey: "key-duplicate",
            cancellationToken: CancellationToken.None);

        first.Should().NotBeNull();
        second.Should().BeNull();
    }
}
