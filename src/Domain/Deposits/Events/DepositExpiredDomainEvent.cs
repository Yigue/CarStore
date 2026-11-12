using SharedKernel;

namespace Domain.Deposits.Events;

/// <summary>RN-003: the handler releases the car only if no other holder remains.</summary>
public sealed record DepositExpiredDomainEvent(Guid DepositId, Guid CarId) : IDomainEvent;
