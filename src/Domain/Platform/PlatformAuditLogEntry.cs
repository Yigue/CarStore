using SharedKernel;

namespace Domain.Platform;

public sealed class PlatformAuditLogEntry : Entity
{
    public const string SourceEventKeyUniqueIndex = "ux_platform_audit_logs_source_event_key";

    private PlatformAuditLogEntry() { }

    public Guid DealerSettingsId { get; private set; }
    public string DealerName { get; private set; } = string.Empty;
    public PlatformAuditAction Action { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string? ActorEmail { get; private set; }
    public PlatformAuditActorKind ActorKind { get; private set; }
    public string? Reason { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public string SourceEventKey { get; private set; } = string.Empty;

    public static PlatformAuditLogEntry Record(
        Guid dealerId,
        Guid dealerSettingsId,
        string dealerName,
        PlatformAuditAction action,
        Guid actorUserId,
        string? actorEmail,
        PlatformAuditActorKind actorKind,
        DateTime occurredAtUtc,
        DateTime recordedAtUtc,
        string sourceEventKey,
        string? reason = null)
    {
        if (actorUserId == Guid.Empty)
            throw new DomainException("PlatformAuditLogEntry requires a non-empty ActorUserId.");
        if (dealerSettingsId == Guid.Empty)
            throw new DomainException("PlatformAuditLogEntry requires a non-empty DealerSettingsId.");
        if (string.IsNullOrWhiteSpace(dealerName))
            throw new DomainException("PlatformAuditLogEntry requires a DealerName snapshot.");
        if (string.IsNullOrWhiteSpace(sourceEventKey))
            throw new DomainException("PlatformAuditLogEntry requires a SourceEventKey.");
        if (occurredAtUtc == default)
            throw new DomainException("PlatformAuditLogEntry requires a real OccurredAtUtc.");

        var entry = new PlatformAuditLogEntry
        {
            Id = Guid.NewGuid(),
            DealerSettingsId = dealerSettingsId,
            DealerName = dealerName,
            Action = action,
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            ActorKind = actorKind,
            OccurredAtUtc = occurredAtUtc,
            RecordedAtUtc = recordedAtUtc,
            SourceEventKey = sourceEventKey,
            Reason = reason,
        };

        entry.SetDealer(dealerId);
        return entry;
    }
}
