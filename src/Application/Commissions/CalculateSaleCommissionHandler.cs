using Application.Abstractions.Data;
using Domain.Commissions;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Commissions;

/// <summary>
/// sfc B4.1 / RN-008 / RN-009: on <see cref="SaleCompletedDomainEvent"/>, snapshots the salesperson
/// commission for the sale. Not to be confused with consignment commission (sfc B7.1 / RN-036,
/// <c>SaleCompletedDomainEventHandler</c>): that is what the dealer earns for selling a THIRD
/// PARTY'S consigned car; this is what the dealer pays its OWN salesperson. The two handlers react
/// to the same event independently and never touch each other's tables.
/// <para>
/// Idempotent by <c>SaleId</c>: checked here before inserting (replayed event / retried handler),
/// and backed by a unique index (Infrastructure) as the last-resort guard against a concurrent race
/// — <see cref="DbUpdateException"/> on that race is swallowed the same way, never thrown.
/// </para>
/// </summary>
internal sealed class CalculateSaleCommissionHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ILogger<CalculateSaleCommissionHandler> logger)
    : INotificationHandler<SaleCompletedDomainEvent>
{
    public async Task Handle(SaleCompletedDomainEvent notification, CancellationToken cancellationToken)
    {
        bool alreadyCalculated = await context.SaleCommissions
            .AnyAsync(c => c.SaleId == notification.SaleId, cancellationToken);
        if (alreadyCalculated)
        {
            logger.LogInformation(
                "SaleCommission already exists for Sale {SaleId}; skipping (idempotent).", notification.SaleId);
            return;
        }

        Sale? sale = await context.Sales.FirstOrDefaultAsync(s => s.Id == notification.SaleId, cancellationToken);
        if (sale is null)
        {
            logger.LogWarning("Sale {SaleId} not found when calculating its commission.", notification.SaleId);
            return;
        }

        // Security review: domain events travel through the outbox, so this can run after the sale was
        // already voided (completed-then-corrected in quick succession). The row must come out already
        // Voided, never Pending/payable, for a sale that is Voided by the time this runs.
        bool saleWasAlreadyVoided = sale.Status == SaleStatus.Voided;

        CommissionPolicy? policy = await context.CommissionPolicies
            .FirstOrDefaultAsync(p => p.DealerId == sale.DealerId, cancellationToken);

        // D-COMMISSION / RN-009: no policy row at all → null rule → 0%, "not configured".
        // A policy row (even at its 0% default) is an explicit choice → IsConfigured = true.
        CommissionRule? rule = policy is null ? null : CommissionRule.Percentage(policy.RateFor(sale.SalespersonId));

        Result<CommissionResult> calculation = CommissionCalculator.Calculate(sale.FinalPrice, sale.TradeInValue, rule);
        if (calculation.IsFailure)
        {
            logger.LogWarning(
                "Commission calculation failed for Sale {SaleId}: {Error}", notification.SaleId, calculation.Error);
            return;
        }

        SaleCommission commission = SaleCommission.Create(
            sale.DealerId, sale.Id, sale.SalespersonId, calculation.Value, dateTimeProvider.UtcNow);
        if (saleWasAlreadyVoided)
        {
            commission.Void();
        }

        context.SaleCommissions.Add(commission);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Security review: this handler runs inside ProcessOutboxMessagesJob, which reuses one DbContext
            // across a whole batch of up to 20 messages and calls SaveChangesAsync again, unconditionally,
            // after every handler has run. A failed insert that stays tracked as Added would fail that
            // trailing save too and poison every other message in the batch — detach it first, always.
            context.DetachEntity(commission);

            bool raceWasTheCause = await context.SaleCommissions
                .AnyAsync(c => c.SaleId == notification.SaleId, cancellationToken);
            if (!raceWasTheCause)
            {
                throw;
            }

            // Lost the race on the SaleId unique index: another call already inserted it. Idempotent no-op.
            logger.LogInformation(
                "SaleCommission unique-index race for Sale {SaleId}; treated as idempotent no-op.", notification.SaleId);
        }
    }
}
