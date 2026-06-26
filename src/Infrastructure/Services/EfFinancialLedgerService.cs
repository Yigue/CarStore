using Application.Abstractions;
using Application.Abstractions.Caching;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Domain.Financial;
using Domain.Financial.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Real EF-backed implementation of <see cref="IFinancialLedgerService"/>.
/// Replaces the previous NoOp stub that swallowed reconditioning expenses.
///
/// Idempotency is enforced in three layers (defense in depth):
///   1. Handler-level \`FirstOrDefaultAsync\` check (fast path; avoids a write).
///   2. DB unique partial index \`IX_transactions_ReconditioningTaskId_SourceId\`
///      with WHERE filter (race-condition floor).
///   3. \`DbUpdateException\` catch around \`SaveChangesAsync\` — re-query for the
///      existing row before throwing, so concurrent outbox replays converge to
///      "already applied" instead of bubbling the exception.
///
/// REQ-FIN-LEDGER-001 (financial/spec.md + enterprise-erp-crm/spec.md
/// SCENARIO 3.B / 3.D).
/// </summary>
internal sealed class EfFinancialLedgerService(
    IApplicationDbContext context,
    ICachedCategoryService cachedCategoryService,
    ICurrentTenantService tenantService,
    ILogger<EfFinancialLedgerService> logger)
    : IFinancialLedgerService
{
    public async Task RegisterExpenseAsync(
        Guid carId,
        decimal amount,
        string currency,
        string category,
        DateTime occurredAt,
        Guid reconditioningTaskId,
        Guid sourceId,
        CancellationToken cancellationToken)
    {
        // Layer 1 — fast path: skip the insert if the row already exists.
        var existing = await context.Transactions
            .FirstOrDefaultAsync(
                t => t.ReconditioningTaskId == reconditioningTaskId
                     && t.SourceId == sourceId,
                cancellationToken);

        if (existing is not null)
        {
            logger.LogDebug(
                "Ledger idempotent hit. ReconditioningTaskId={ReconditioningTaskId} SourceId={SourceId}",
                reconditioningTaskId, sourceId);
            return;
        }

        // Resolve the category by canonical name. The seeder ensures
        // Reconditioning and VehicleSale exist on every install.
        var categoryEntity = await cachedCategoryService.GetByNameAsync(category, cancellationToken);
        if (categoryEntity is null)
        {
            // Should never happen in a seeded DB; defensive fail-safe.
            categoryEntity = new TransactionCategory(category, string.Empty, TransactionType.Expense);
            context.TransactionCategories.Add(categoryEntity);
        }

        var transaction = new FinancialTransaction(
            dealerId: tenantService.DealerId,
            type: TransactionType.Expense,
            amount: amount,
            description: $"Reconditioning {category}",
            paymentMethod: PaymentMethod.Other,
            category: categoryEntity,
            transactionDate: occurredAt,
            reconditioningTaskId: reconditioningTaskId,
            sourceId: sourceId);

        context.Transactions.Add(transaction);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Layer 3 — race-condition floor: a concurrent insert may have
            // already committed the same key. Re-query before re-throwing
            // so the outbox replay can mark the event as already applied.
            var raceWinner = await context.Transactions.AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.ReconditioningTaskId == reconditioningTaskId
                         && t.SourceId == sourceId,
                    cancellationToken);

            if (raceWinner is not null)
            {
                logger.LogDebug(
                    "Ledger idempotent race. ReconditioningTaskId={ReconditioningTaskId} SourceId={SourceId} ExistingId={ExistingId}",
                    reconditioningTaskId, sourceId, raceWinner.Id);
                return;
            }

            logger.LogError(ex,
                "Ledger insert failed and no idempotency row was found. ReconditioningTaskId={ReconditioningTaskId} SourceId={SourceId}",
                reconditioningTaskId, sourceId);
            throw;
        }

        // Layer 4 — keep the cached all-list fresh after we mutated it.
        await cachedCategoryService.InvalidateCacheAsync(cancellationToken);
    }
}