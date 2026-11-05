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

    /// <summary>
    /// spr PR 3.1 (ADR B8, D2): UTC instant this cache first observed <see cref="SubscriptionStatus.PastDue"/>
    /// for this dealer, used as the anchor for <c>Domain.Billing.DealerOperationalStatusResolver.DecideAccess</c>'s
    /// grace window. Null when the dealer is not currently PastDue, or when this cache never recorded the
    /// anchor (e.g. after a full cache flush) — callers MUST treat null as "unknown" and fail closed,
    /// exactly like <c>DecideAccess</c> already does for an unknown <c>pastDueSinceUtc</c>.
    /// </summary>
    Task<DateTime?> GetPastDueSinceAsync(Guid dealerId, CancellationToken ct = default);
}
