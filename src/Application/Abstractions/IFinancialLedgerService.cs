namespace Application.Abstractions;

/// <summary>
/// Abstraction over the financial ledger. Implementations must be IDEMPOTENT
/// per <paramref name="sourceId"/> — calling RegisterExpenseAsync twice with the
/// same sourceId MUST NOT create duplicate entries.
///
/// PHASE-4: real implementation is out of scope; see NoOpFinancialLedgerService.
/// </summary>
public interface IFinancialLedgerService
{
    Task RegisterExpenseAsync(
        Guid carId,
        decimal amount,
        string currency,
        string category,
        DateTime occurredAt,
        Guid sourceId,
        CancellationToken cancellationToken);
}
