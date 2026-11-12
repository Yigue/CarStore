using SharedKernel;

namespace Domain.Deposits.Events;

public sealed record DepositCreatedDomainEvent(Guid DepositId, Guid CarId, Guid? QuoteId) : IDomainEvent;
