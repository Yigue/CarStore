using SharedKernel;

namespace Domain.Financial.Events;

public sealed record TransactionUpdatedDomainEvent(Guid TransactionId) : IDomainEvent;
