using SharedKernel;

namespace Domain.Clients.Events;

public sealed record ClientSoftDeletedDomainEvent(
    Guid ClientId,
    DateTime DeletedAtUtc,
    Guid? DeletedBy) : IDomainEvent;
