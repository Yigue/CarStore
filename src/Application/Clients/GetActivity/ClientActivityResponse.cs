namespace Application.Clients.GetActivity;

public sealed record ClientActivityResponse(
    IReadOnlyList<ActivityEntry> Items,
    int TotalCount);

/// <summary>
/// One entry in the client's timeline.
///
/// <para>
/// This used to be <c>(Id, EventType, OccurredAtUtc)</c> — an event class name and a date. The UI
/// rendered a column of labels with no way to tell which sale or quote each line meant, which is
/// why the screen looked implemented but told nobody anything. <see cref="Description"/> is the
/// readable sentence, and the related pair lets the UI link the row to the thing it happened to.
/// </para>
/// </summary>
public sealed record ActivityEntry(
    Guid Id,
    /// <summary>Raw event class name. Kept so existing consumers keep working.</summary>
    string EventType,
    string Description,
    Guid? RelatedEntityId,
    string? RelatedEntityType,
    DateTime OccurredAtUtc);
