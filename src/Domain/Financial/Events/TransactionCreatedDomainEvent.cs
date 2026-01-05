using SharedKernel;
namespace Domain.Financial.Events;
public sealed record TransactionCreatedDomainEvent(Guid TransactionId) : IDomainEvent;
