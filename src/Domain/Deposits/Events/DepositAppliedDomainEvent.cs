using SharedKernel;

namespace Domain.Deposits.Events;

public sealed record DepositAppliedDomainEvent(Guid DepositId, Guid CarId, Guid SaleId) : IDomainEvent;
