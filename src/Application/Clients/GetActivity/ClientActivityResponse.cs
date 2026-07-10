namespace Application.Clients.GetActivity;

public sealed record ClientActivityResponse(
    IReadOnlyList<ActivityEntry> Items,
    int TotalCount);

public sealed record ActivityEntry(
    Guid Id,
    string EventType,
    DateTime OccurredAtUtc);
