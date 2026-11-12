using SharedKernel;

namespace Domain.Auditing;

/// <summary>
/// sfc B2.2 / ADR C10 / RN-025: one audited property change of a tenant entity (who changed what, from what to what,
/// and why). Tenant-scoped and a separate table from <c>PlatformAuditLogEntry</c> (platform staff actions). Written by
/// <c>TenantAuditInterceptor</c> in the SAME SaveChanges (so the same transaction) as the change it describes, and
/// retained by policy: the tenant purge does not delete it.
/// </summary>
public sealed class DealerAuditLogEntry : Entity
{
    /// <summary>Actor of a change made outside a request (a job, a webhook): there is no signed-in person.</summary>
    public static readonly Guid SystemActorId = new("00000000-0000-0000-0000-00000000c0de");

    public const int MaxValueLength = 500;

    private DealerAuditLogEntry() { }

    /// <summary>CLR name of the audited entity, e.g. <c>Car</c>.</summary>
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Property { get; private set; } = string.Empty;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public Guid ActorUserId { get; private set; }

    /// <summary>Business reason, e.g. the void reason of a sale (set through <c>IAuditContext.SetReason</c>).</summary>
    public string? Reason { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }

    public static DealerAuditLogEntry Record(
        Guid dealerId,
        string entityType,
        Guid entityId,
        string property,
        string? oldValue,
        string? newValue,
        Guid actorUserId,
        string? reason,
        string correlationId,
        DateTime occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new DomainException("DealerAuditLogEntry requires an EntityType.");
        if (string.IsNullOrWhiteSpace(property))
            throw new DomainException("DealerAuditLogEntry requires a Property.");
        if (entityId == Guid.Empty)
            throw new DomainException("DealerAuditLogEntry requires a non-empty EntityId.");
        if (occurredAtUtc == default)
            throw new DomainException("DealerAuditLogEntry requires a real OccurredAtUtc.");

        var entry = new DealerAuditLogEntry
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            Property = property,
            OldValue = Truncate(oldValue),
            NewValue = Truncate(newValue),
            ActorUserId = actorUserId,
            Reason = Truncate(reason),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            OccurredAtUtc = occurredAtUtc,
        };

        entry.SetDealer(dealerId);
        return entry;
    }

    private static string? Truncate(string? value) =>
        value is { Length: > MaxValueLength } ? value[..MaxValueLength] : value;
}
