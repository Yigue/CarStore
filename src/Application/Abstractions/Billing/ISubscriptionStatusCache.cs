using Domain.Billing;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions.Billing;

public interface ISubscriptionStatusCache
{
    Task<SubscriptionStatus?> GetAsync(Guid dealerId, CancellationToken ct = default);
    Task SetAsync(Guid dealerId, SubscriptionStatus status, TimeSpan ttl, CancellationToken ct = default);
    Task InvalidateAsync(Guid dealerId, CancellationToken ct = default);
}
