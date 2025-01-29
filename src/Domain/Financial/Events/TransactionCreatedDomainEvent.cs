using SharedKernel;
using Domain.Financial.Attributes;

namespace Domain.Financial.Events;

public sealed record TransactionCreatedDomainEvent(
    Guid TransactionId,
    TransactionType Type,
    decimal Amount) : IDomainEvent;
