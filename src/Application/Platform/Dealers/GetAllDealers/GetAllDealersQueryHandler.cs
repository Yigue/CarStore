using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Platform.Common;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Platform.Dealers.GetAllDealers;

internal sealed class GetAllDealersQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllDealersQuery, IReadOnlyList<PlatformDealerResponse>>
{
    public async Task<Result<IReadOnlyList<PlatformDealerResponse>>> Handle(
        GetAllDealersQuery query,
        CancellationToken cancellationToken)
    {
        var entities = await context.DealerSettings
            .IgnoreQueryFilters()
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        // spr PR 2.1 (BE-PLATFORM-06/07, D3): one dealer has at most one subscription row, but the
        // lookup is defensive (first-wins) in case historical data ever has more than one.
        var subscriptions = await context.DealerSubscriptions
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var subscriptionsByDealerId = subscriptions
            .GroupBy(s => s.DealerId)
            .ToDictionary(g => g.Key, g => g.First());

        var dealers = entities
            .Select(d =>
            {
                subscriptionsByDealerId.TryGetValue(d.DealerId, out var subscription);
                var billingStanding = BillingStandingMapper.From(subscription?.Status);
                var operationalStatus = DealerOperationalStatusResolver.Resolve(d.IsActive, billingStanding);

                return new PlatformDealerResponse(
                    d.Id,
                    d.DealerId,
                    d.DealerName,
                    d.ContactEmail,
                    d.IsActive,
                    d.SuspendedAt,
                    d.SuspendReason,
                    d.CreatedAt,
                    d.CustomDomain,
                    "v" + d.RowVersion.ToString(),
                    subscription?.Status,
                    subscription?.PlanCode,
                    subscription?.TrialEndsAt,
                    subscription?.CurrentPeriodEnd,
                    operationalStatus);
            })
            .ToList();

        return Result.Success<IReadOnlyList<PlatformDealerResponse>>(dealers);
    }
}
