using SharedKernel;

namespace Domain.Leads;

/// <summary>
/// One entry in a lead's history.
///
/// <para>
/// A dedicated table rather than a projection over <c>OutboxMessages</c>, which is how the client
/// timeline is built. The outbox is delivery infrastructure: rows are processed and purged, and
/// its projection carries only an event name and a timestamp — which is exactly why the client
/// timeline reads as a list of labels nobody can act on. A lead's history is domain data that has
/// to outlive message delivery and carry enough context to be read months later.
/// </para>
///
/// <para>
/// Written only by application-layer domain event handlers, never by a user-facing command, so
/// the history records what happened rather than what someone typed.
/// </para>
/// </summary>
public sealed class LeadActivity : Entity
{
    public Guid LeadId { get; private set; }

    public LeadActivityType Type { get; private set; }

    /// <summary>Human-readable sentence, already in the dealership's language.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>The quote, sale, appointment or client this entry refers to, when there is one.</summary>
    public Guid? RelatedEntityId { get; private set; }

    /// <summary>
    /// Type name of <see cref="RelatedEntityId"/> ("Quote", "Sale", …). Stored rather than
    /// inferred from <see cref="Type"/> so the UI can build a link without a mapping table that
    /// would silently rot as new activity types appear.
    /// </summary>
    public string? RelatedEntityType { get; private set; }

    /// <summary>Null for system-driven entries: an outbox retry has no acting user.</summary>
    public Guid? ActorUserId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    // EF Core.
    private LeadActivity()
    {
    }

    public static LeadActivity Record(
        Guid dealerId,
        Guid leadId,
        LeadActivityType type,
        string description,
        DateTime occurredAtUtc,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null,
        Guid? actorUserId = null)
    {
        if (leadId == Guid.Empty)
        {
            throw new DomainException("LeadActivity requires a non-empty LeadId.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("LeadActivity requires a description.");
        }

        // A related id without its type is a dead link: the UI cannot route it anywhere.
        if (relatedEntityId.HasValue && string.IsNullOrWhiteSpace(relatedEntityType))
        {
            throw new DomainException("LeadActivity requires RelatedEntityType when RelatedEntityId is set.");
        }

        var activity = new LeadActivity
        {
            Id = Guid.NewGuid(),
            LeadId = leadId,
            Type = type,
            Description = description.Trim(),
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityId.HasValue ? relatedEntityType : null,
            ActorUserId = actorUserId,
            OccurredAtUtc = occurredAtUtc,
        };

        activity.SetDealer(dealerId);
        return activity;
    }
}
