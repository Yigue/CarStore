using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Platform.Common;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Platform.Metrics.GetPlatformMetrics;

internal sealed class GetPlatformMetricsQueryHandler(IApplicationDbContext context, IPlanCatalog planCatalog)
    : IQueryHandler<GetPlatformMetricsQuery, PlatformMetricsResponse>
{
    /// <summary>
    /// spr PR 1.3 (SPR-04/FE-SA-MET-02): a <see cref="SubscriptionStatus.PastDue"/> dealer is still
    /// "expected" revenue until it lapses into Suspended/Cancelled — it keeps counting toward MRR.
    /// </summary>
    private const string DefaultMrrCurrency = "USD";

    public async Task<Result<PlatformMetricsResponse>> Handle(
        GetPlatformMetricsQuery query,
        CancellationToken cancellationToken)
    {
        var dealers = await context.DealerSettings
            .IgnoreQueryFilters()
            .Select(d => d.IsActive)
            .ToListAsync(cancellationToken);

        var total = dealers.Count;
        var active = dealers.Count(isActive => isActive);
        var suspended = total - active;

        var totalUsers = await context.Users.IgnoreQueryFilters().CountAsync(cancellationToken);

        var mrr = await ComputeMrrAsync(cancellationToken);

        return Result.Success(new PlatformMetricsResponse(
            total,
            active,
            suspended,
            totalUsers,
            mrr));
    }

    private async Task<MrrStub> ComputeMrrAsync(CancellationToken cancellationToken)
    {
        var planCodes = await context.DealerSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Active
                || s.Status == SubscriptionStatus.Trialing
                || s.Status == SubscriptionStatus.PastDue)
            .Select(s => s.PlanCode)
            .ToListAsync(cancellationToken);

        if (planCodes.Count == 0)
        {
            return new MrrStub(0m, DefaultMrrCurrency, false, null);
        }

        long totalMinor = 0;
        string? currency = null;

        foreach (var group in planCodes.GroupBy(code => code))
        {
            var plan = await planCatalog.GetAsync(group.Key, cancellationToken);
            if (plan is null)
            {
                // Defensive only: PlanCode is only ever set from an IsSellable-validated code, so the
                // catalog should always resolve it. Skip rather than let one missing plan crash the
                // whole platform metrics query.
                continue;
            }

            totalMinor += plan.AmountMinor * group.Count();
            currency ??= plan.Currency;
        }

        return new MrrStub(totalMinor / 100m, currency ?? DefaultMrrCurrency, false, null);
    }
}
