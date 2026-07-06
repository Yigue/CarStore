using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

internal sealed class DealerSubscriptionRepository : IDealerSubscriptionRepository
{
    private readonly IApplicationDbContext _context;

    public DealerSubscriptionRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DealerSubscription?> GetByDealerIdAsync(Guid dealerId, CancellationToken ct = default)
    {
        return await _context.DealerSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.DealerId == dealerId, ct);
    }

    public async Task<DealerSubscription?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct = default)
    {
        return await _context.DealerSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.StripeCustomerId == stripeCustomerId, ct);
    }

    public async Task AddAsync(DealerSubscription subscription, CancellationToken ct = default)
    {
        await _context.DealerSubscriptions.AddAsync(subscription, ct);
    }

    public void Update(DealerSubscription subscription)
    {
        _context.DealerSubscriptions.Update(subscription);
    }
}
