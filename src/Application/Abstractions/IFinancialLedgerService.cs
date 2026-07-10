namespace Application.Abstractions;

/// <summary>
/// Abstraction over the financial ledger. Implementations MUST be IDEMPOTENT
/// keyed by the composite <c>(reconditioningTaskId, sourceId)</c> — calling
/// <see cref="RegisterExpenseAsync"/> twice with the same pair MUST NOT create
/// duplicate entries. Outbox replays and concurrent handler invocations MUST
/// converge to one row.
///
/// REQ-FIN-LEDGER-001 (financial/spec.md + enterprise-erp-crm/spec.md
/// SCENARIO 3.B / 3.D).
/// </summary>
public interface IFinancialLedgerService
{
    Task RegisterExpenseAsync(
        Guid carId,
        decimal amount,
        string currency,
        string category,
        DateTime occurredAt,
        Guid reconditioningTaskId,
        Guid sourceId,
        CancellationToken cancellationToken);
}
