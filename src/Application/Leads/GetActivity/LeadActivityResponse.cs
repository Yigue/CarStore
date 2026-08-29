using Domain.Leads;

namespace Application.Leads.GetActivity;

/// <summary>
/// One timeline entry. Unlike the client activity feed — which projects only an event name and a
/// timestamp out of the outbox and therefore reads as a list of labels — this carries the sentence
/// and the entity it refers to, so the UI can render something a person can act on.
/// </summary>
public sealed record LeadActivityEntry(
    Guid Id,
    LeadActivityType Type,
    string Description,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    Guid? ActorUserId,
    DateTime OccurredAtUtc);

public sealed record LeadActivityResponse(
    IReadOnlyList<LeadActivityEntry> Items,
    int TotalCount);
