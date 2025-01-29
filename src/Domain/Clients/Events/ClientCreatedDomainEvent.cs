using SharedKernel;

namespace Domain.Clients.Events;

public sealed record ClientCreatedDomainEvent(
    Guid ClientId,
    string FullName) : IDomainEvent;
