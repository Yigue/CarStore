using Domain.Billing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Billing;

public interface IDealerSubscriptionRepository
{
    Task<DealerSubscription?> GetByDealerIdAsync(Guid dealerId, CancellationToken ct = default);
    Task<DealerSubscription?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken ct = default);
    Task AddAsync(DealerSubscription subscription, CancellationToken ct = default);
    void Update(DealerSubscription subscription);
}
