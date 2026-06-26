using Domain.Clients.Attributes;
using SharedKernel;

namespace Domain.Clients.Events;

/// <summary>
/// Raised when a Client's <see cref="ClientType"/> changes from one value to another.
/// </summary>
public sealed record ClientTypeChangedDomainEvent(
    Guid ClientId,
    ClientType From,
    ClientType To,
    DateTime OccurredAtUtc) : IDomainEvent;