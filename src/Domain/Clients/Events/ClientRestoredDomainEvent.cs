using SharedKernel;

namespace Domain.Clients.Events;

public sealed record ClientRestoredDomainEvent(
    Guid ClientId,
    DateTime RestoredAtUtc,
    Guid? RestoredBy) : IDomainEvent;
