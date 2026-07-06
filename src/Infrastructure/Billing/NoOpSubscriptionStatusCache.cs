using Application.Abstractions.Billing;
using Domain.Billing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

public class NoOpSubscriptionStatusCache : ISubscriptionStatusCache
{
    public Task<SubscriptionStatus?> GetAsync(Guid dealerId, CancellationToken ct = default)
    {
        return Task.FromResult<SubscriptionStatus?>(null);
    }

    public Task SetAsync(Guid dealerId, SubscriptionStatus status, TimeSpan ttl, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(Guid dealerId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
