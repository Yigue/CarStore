using SharedKernel;

namespace Domain.Clients.Events;

public sealed record ClientNotesUpdatedDomainEvent(
    Guid ClientId,
    DateTime OccurredAt,
    Guid? ActorId) : IDomainEvent;
