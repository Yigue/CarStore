using System;
using System.Linq;
using System.Reflection;
using Domain.Platform;
using FluentAssertions;
using SharedKernel;
using Xunit;

namespace DomainTests.Platform;

public class PlatformAuditLogEntryTests
{
    private static (Guid dealerId, Guid dealerSettingsId, string dealerName, PlatformAuditAction action, Guid actorUserId, string actorEmail, PlatformAuditActorKind actorKind, DateTime occurredAt, DateTime recordedAt, string sourceKey) ValidParams()
        => (Guid.NewGuid(), Guid.NewGuid(), "Test Dealer", PlatformAuditAction.DealerSuspended, Guid.NewGuid(), "admin@carstore.com", PlatformAuditActorKind.SuperAdmin, DateTime.UtcNow, DateTime.UtcNow, "key-1");

    [Fact]
    public void Record_WithValidParams_CreatesEntry()
    {
        var p = ValidParams();
        var entry = PlatformAuditLogEntry.Record(
            p.dealerId, p.dealerSettingsId, p.dealerName, p.action, p.actorUserId, p.actorEmail, p.actorKind, p.occurredAt, p.recordedAt, p.sourceKey, "Reason");

        entry.DealerId.Should().Be(p.dealerId);
        entry.DealerSettingsId.Should().Be(p.dealerSettingsId);
        entry.DealerName.Should().Be(p.dealerName);
        entry.Action.Should().Be(p.action);
        entry.ActorUserId.Should().Be(p.actorUserId);
        entry.ActorEmail.Should().Be(p.actorEmail);
        entry.ActorKind.Should().Be(p.actorKind);
        entry.OccurredAtUtc.Should().Be(p.occurredAt);
        entry.RecordedAtUtc.Should().Be(p.recordedAt);
        entry.SourceEventKey.Should().Be(p.sourceKey);
        entry.Reason.Should().Be("Reason");
    }

    [Fact]
    public void Record_WithEmptyActorUserId_Throws()
    {
        var p = ValidParams();
        var act = () => PlatformAuditLogEntry.Record(
            p.dealerId, p.dealerSettingsId, p.dealerName, p.action, Guid.Empty, p.actorEmail, p.actorKind, p.occurredAt, p.recordedAt, p.sourceKey);

        act.Should().Throw<DomainException>().WithMessage("*ActorUserId*");
    }

    [Fact]
    public void Record_WithEmptySourceEventKey_Throws()
    {
        var p = ValidParams();
        var act = () => PlatformAuditLogEntry.Record(
            p.dealerId, p.dealerSettingsId, p.dealerName, p.action, p.actorUserId, p.actorEmail, p.actorKind, p.occurredAt, p.recordedAt, "");

        act.Should().Throw<DomainException>().WithMessage("*SourceEventKey*");
    }

    [Fact]
    public void Record_WithoutDealerName_Throws()
    {
        var p = ValidParams();
        var act = () => PlatformAuditLogEntry.Record(
            p.dealerId, p.dealerSettingsId, "  ", p.action, p.actorUserId, p.actorEmail, p.actorKind, p.occurredAt, p.recordedAt, p.sourceKey);

        act.Should().Throw<DomainException>().WithMessage("*DealerName*");
    }

    [Fact]
    public void Record_WithEmptyDealerSettingsId_Throws()
    {
        var p = ValidParams();
        var act = () => PlatformAuditLogEntry.Record(
            p.dealerId, Guid.Empty, p.dealerName, p.action, p.actorUserId, p.actorEmail, p.actorKind, p.occurredAt, p.recordedAt, p.sourceKey);

        act.Should().Throw<DomainException>().WithMessage("*DealerSettingsId*");
    }

    [Fact]
    public void PlatformAuditLogEntry_ExposesNoMutators()
    {
        var type = typeof(PlatformAuditLogEntry);
        var publicSetters = type.GetProperties()
            .Where(p => p.SetMethod != null && p.SetMethod.IsPublic);

        publicSetters.Should().BeEmpty();

        var publicMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName); // Exclude property getters

        publicMethods.Should().BeEmpty();
    }
}
