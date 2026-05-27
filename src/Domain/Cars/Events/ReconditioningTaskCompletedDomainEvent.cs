using SharedKernel;

namespace Domain.Cars.Events;

/// <summary>
/// Raised when a reconditioning task is marked as completed.
/// Consumed by the financial ledger to register the cost as an expense.
/// </summary>
public sealed record ReconditioningTaskCompletedDomainEvent(
    Guid CarId,
    Guid TaskId,
    decimal CostAmount,
    string Currency,
    DateTime CompletedAt) : IDomainEvent;
