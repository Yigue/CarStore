using Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// PHASE-4: NoOp ledger. Logs the call and returns. A real implementation should
/// upsert into <c>financial_transactions</c> keyed by <paramref name="sourceId"/>
/// for idempotency. Wiring of the real ledger is intentionally out of scope.
/// </summary>
internal sealed class NoOpFinancialLedgerService(ILogger<NoOpFinancialLedgerService> logger)
    : IFinancialLedgerService
{
    public Task RegisterExpenseAsync(
        Guid carId,
        decimal amount,
        string currency,
        string category,
        DateTime occurredAt,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[NoOp Ledger] Expense pending wire-up: CarId={CarId} Amount={Amount} {Currency} Category={Category} OccurredAt={OccurredAt} SourceId={SourceId}",
            carId, amount, currency, category, occurredAt, sourceId);

        return Task.CompletedTask;
    }
}
