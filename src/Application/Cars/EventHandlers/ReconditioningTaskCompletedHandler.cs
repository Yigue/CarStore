using Application.Abstractions;
using Domain.Cars.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Cars.EventHandlers;

/// <summary>
/// On task completion, register the cost as a "Reconditioning" expense in the financial ledger.
/// IDEMPOTENT: <see cref="IFinancialLedgerService.RegisterExpenseAsync"/> is required to
/// upsert by <c>sourceId</c> (the TaskId), so replays from the outbox pattern are safe.
/// </summary>
internal sealed class ReconditioningTaskCompletedHandler(
    IFinancialLedgerService ledger,
    ILogger<ReconditioningTaskCompletedHandler> logger)
    : INotificationHandler<ReconditioningTaskCompletedDomainEvent>
{
    private const string Category = "Reconditioning";

    public async Task Handle(
        ReconditioningTaskCompletedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Reconditioning task completed. CarId={CarId} TaskId={TaskId} Amount={Amount} {Currency}",
            notification.CarId,
            notification.TaskId,
            notification.CostAmount,
            notification.Currency);

        await ledger.RegisterExpenseAsync(
            carId: notification.CarId,
            amount: notification.CostAmount,
            currency: notification.Currency,
            category: Category,
            occurredAt: notification.CompletedAt,
            // REQ-FIN-LEDGER-001: composite idempotency key. Today both fields
            // carry the same TaskId; the composite key leaves room for future
            // partial-completion flows that derive a divergent SourceId.
            reconditioningTaskId: notification.TaskId,
            sourceId: notification.TaskId,
            cancellationToken: cancellationToken);
    }
}
