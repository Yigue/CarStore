using SharedKernel;

namespace Domain.Clients.Events;

public sealed record ClientUpdatedDomainEvent(Guid ClientId) : IDomainEvent;
