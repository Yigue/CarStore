using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Billing.Commands.ReconcileSubscriptions;

/// <summary>
/// spr ADR B9. Finds dealers with no CONFIRMED subscription — no row at all (legacy) or an
/// <see cref="SubscriptionStatus.Incomplete"/> row with no Stripe subscription — that have been in
/// that state longer than the grace window. For each one it first tries to heal from Stripe (the
/// provider is the source of truth); if the provider has nothing it records a persisted
/// <see cref="BillingReconciliationFlag"/> and emits ONE alert. It never deletes or suspends:
/// the dealer stays behind the 402 gate and a human decides.
/// </summary>
internal sealed class ReconcileSubscriptionsCommandHandler(
    IApplicationDbContext context,
    ISubscriptionGateway gateway,
    IDateTimeProvider clock,
    ILogger<ReconcileSubscriptionsCommandHandler> logger)
    : ICommandHandler<ReconcileSubscriptionsCommand, ReconcileSubscriptionsResult>
{
    public async Task<Result<ReconcileSubscriptionsResult>> Handle(
        ReconcileSubscriptionsCommand command,
        CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow;
        DateTime cutoff = now - command.Grace;
        int batch = Math.Max(1, command.BatchSize);

        // 1. Clear flags for dealers that are confirmed by now.
        int cleared = await ClearResolvedFlagsAsync(now, cancellationToken);

        // 2. Candidates: pending rows older than the grace, plus legacy dealers with no row at all.
        var pending = await context.DealerSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Incomplete
                        && s.StripeSubscriptionId == null
                        && s.CurrentPeriodStart <= cutoff)
            .OrderBy(s => s.CurrentPeriodStart)
            .Take(batch)
            .ToListAsync(cancellationToken);

        var withoutRow = new List<Guid>();
        int remaining = batch - pending.Count;
        if (remaining > 0)
        {
            withoutRow = await context.DealerSettings
                .IgnoreQueryFilters()
                .Where(d => d.CreatedAt <= cutoff
                            && !context.DealerSubscriptions.IgnoreQueryFilters().Any(s => s.DealerId == d.DealerId))
                .OrderBy(d => d.CreatedAt)
                .Select(d => d.DealerId)
                .Take(remaining)
                .ToListAsync(cancellationToken);
        }

        int examined = 0;
        int healed = 0;
        int flagged = 0;

        foreach (DealerSubscription row in pending)
        {
            examined++;
            switch (await TryHealAsync(row.DealerId, row, now, cancellationToken))
            {
                case HealOutcome.Healed:
                    healed++;
                    break;
                case HealOutcome.NotFound
                    when await FlagAsync(row.DealerId, BillingReconciliationFlag.IncompleteAbandoned, now, cancellationToken):
                    flagged++;
                    break;
            }
        }

        foreach (Guid dealerId in withoutRow)
        {
            examined++;
            switch (await TryHealAsync(dealerId, null, now, cancellationToken))
            {
                case HealOutcome.Healed:
                    healed++;
                    break;
                case HealOutcome.NotFound
                    when await FlagAsync(dealerId, BillingReconciliationFlag.NoSubscriptionRow, now, cancellationToken):
                    flagged++;
                    break;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new ReconcileSubscriptionsResult(examined, healed, flagged, cleared));
    }

    private async Task<int> ClearResolvedFlagsAsync(DateTime now, CancellationToken ct)
    {
        List<BillingReconciliationFlag> open = await context.BillingReconciliationFlags
            .Where(f => f.ClearedAtUtc == null)
            .ToListAsync(ct);

        int cleared = 0;
        foreach (BillingReconciliationFlag flag in open)
        {
            DealerSubscription? row = await context.DealerSubscriptions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.DealerId == flag.DealerId, ct);

            bool confirmed = row is not null && row.Status != SubscriptionStatus.Incomplete;
            if (confirmed)
            {
                flag.Clear(now);
                cleared++;
            }
        }

        return cleared;
    }

    /// <summary>Asks Stripe; when it holds a live subscription the local row is brought in line with it.</summary>
    private async Task<HealOutcome> TryHealAsync(Guid dealerId, DealerSubscription? row, DateTime now, CancellationToken ct)
    {
        ExternalSubscription? external;
        try
        {
            external = await gateway.FindSubscriptionForDealerAsync(dealerId, ct);
        }
        catch (Exception ex)
        {
            // Provider unreachable: do not flag on a failed lookup — try again next run.
            logger.LogWarning(ex, "Billing reconciliation: provider lookup failed for dealer {DealerId}; skipping this run.", dealerId);
            return HealOutcome.Unavailable;
        }

        if (external is null)
        {
            return HealOutcome.NotFound;
        }

        if (row is null)
        {
            row = DealerSubscription.CreatePending(dealerId, external.CustomerId, external.PriceId ?? string.Empty);
            await context.DealerSubscriptions.AddAsync(row, ct);
        }

        row.AttachStripeCustomer(external.CustomerId);
        row.SetPlan(external.PriceId ?? string.Empty);

        switch (external.Status)
        {
            case SubscriptionStatus.Trialing:
                row.StartTrial(external.SubscriptionId, external.TrialEnd, external.PeriodStart, external.PeriodEnd);
                break;
            case SubscriptionStatus.Active:
                row.Activate(external.SubscriptionId, external.PeriodStart, external.PeriodEnd);
                break;
            default:
                // The provider has a subscription but it is not paying (past_due/unpaid...): the
                // webhook path owns those transitions; link it so they can find the row.
                return HealOutcome.NotFound;
        }

        BillingReconciliationFlag? flag = await context.BillingReconciliationFlags
            .FirstOrDefaultAsync(f => f.DealerId == dealerId, ct);
        flag?.Clear(now);

        logger.LogInformation("Billing reconciliation: dealer {DealerId} healed from the payment provider ({Status}).",
            dealerId, external.Status);
        return HealOutcome.Healed;
    }

    private enum HealOutcome
    {
        Healed,
        NotFound,
        Unavailable
    }

    /// <returns>True only when a NEW alert was emitted (first time this finding is raised).</returns>
    private async Task<bool> FlagAsync(Guid dealerId, string reason, DateTime now, CancellationToken ct)
    {
        BillingReconciliationFlag? existing = await context.BillingReconciliationFlags
            .FirstOrDefaultAsync(f => f.DealerId == dealerId, ct);

        if (existing is { IsOpen: true })
        {
            return false;
        }

        if (existing is null)
        {
            await context.BillingReconciliationFlags.AddAsync(BillingReconciliationFlag.Open(dealerId, reason, now), ct);
        }
        else
        {
            existing.Reopen(reason, now);
        }

        logger.LogError(
            "Billing reconciliation: dealer {DealerId} has no confirmed subscription after the grace window ({Reason}). " +
            "Flagged ReconciliationPending; the dealer remains blocked (402). Nothing was deleted.",
            dealerId, reason);
        return true;
    }
}
