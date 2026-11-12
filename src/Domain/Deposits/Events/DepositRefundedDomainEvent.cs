using SharedKernel;

namespace Domain.Deposits.Events;

public sealed record DepositRefundedDomainEvent(Guid DepositId, Guid CarId, decimal Amount, string Reason) : IDomainEvent;
