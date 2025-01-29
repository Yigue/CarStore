using SharedKernel;

namespace Domain.Clients.Events;

public sealed record ClientDeactivatedDomainEvent(Guid ClientId) : IDomainEvent;
