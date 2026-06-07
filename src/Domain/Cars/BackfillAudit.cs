using SharedKernel;

namespace Domain.Cars;

/// <summary>
/// One row per backfill invocation. Append-only audit log for REQ-FVIP-1 (admin backfill endpoint).
/// Survives user deletion by design — no FK to users.
/// </summary>
public sealed class BackfillAudit : Entity
{
    public Guid ActorUserId { get; private set; }

    public BackfillAction Action { get; private set; }

    public int AffectedRowCount { get; private set; }

    public int? ExecutionTimeMs { get; private set; }

    /// <summary>JSON metadata: e.g. list of affected car_image ids, or other diagnostic data.</summary>
    public string? MetadataJson { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // EF Core.
    private BackfillAudit()
    {
    }

    public static BackfillAudit Create(
        Guid dealerId,
        Guid actorUserId,
        BackfillAction action,
        int affectedRowCount,
        int? executionTimeMs,
        string? metadataJson,
        DateTime createdAtUtc)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new DomainException("BackfillAudit requires a non-empty ActorUserId.");
        }

        if (affectedRowCount < 0)
        {
            throw new DomainException("BackfillAudit.AffectedRowCount cannot be negative.");
        }

        var audit = new BackfillAudit
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            Action = action,
            AffectedRowCount = affectedRowCount,
            ExecutionTimeMs = executionTimeMs,
            MetadataJson = metadataJson,
            CreatedAtUtc = createdAtUtc,
        };
        audit.SetDealer(dealerId);
        return audit;
    }
}
