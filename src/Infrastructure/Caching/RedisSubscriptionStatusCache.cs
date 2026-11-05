using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Domain.Billing;
using Microsoft.Extensions.Caching.Distributed;

namespace Infrastructure.Caching;

internal sealed class RedisSubscriptionStatusCache : ISubscriptionStatusCache
{
    private readonly IDistributedCache _cache;
    private readonly IDealerSubscriptionRepository _repository;

    public RedisSubscriptionStatusCache(IDistributedCache cache, IDealerSubscriptionRepository repository)
    {
        _cache = cache;
        _repository = repository;
    }

    // spr PR 3.1 (ADR B8, D2): tolerance kept far above any realistic grace window (default 7 days,
    // configurable via Billing:PastDueGraceDays) so the anchor outlives the 300s status TTL above and
    // survives normal cache churn; only a full Redis flush would reset it (documented, accepted tradeoff
    // — see ISubscriptionStatusCache.GetPastDueSinceAsync).
    private static readonly TimeSpan PastDueSinceTtl = TimeSpan.FromDays(60);

    private static string GetKey(Guid dealerId) => $"subscription:status:{dealerId}";

    private static string GetPastDueSinceKey(Guid dealerId) => $"subscription:past-due-since:{dealerId}";

    public async Task<SubscriptionStatus?> GetAsync(Guid dealerId, CancellationToken ct = default)
    {
        var key = GetKey(dealerId);
        var cached = await _cache.GetStringAsync(key, ct);
        
        if (!string.IsNullOrEmpty(cached))
        {
            if (Enum.TryParse<SubscriptionStatus>(cached, out var status))
            {
                Console.WriteLine($"[RedisCache] Cache HIT for {dealerId}. Status: {status}");
                return status;
            }
        }

        var subscription = await _repository.GetByDealerIdAsync(dealerId, ct);
        if (subscription != null)
        {
            Console.WriteLine($"[RedisCache] Cache MISS for {dealerId}. Fetched from DB. Status: {subscription.Status}");
            await SetAsync(dealerId, subscription.Status, TimeSpan.FromSeconds(300), ct);
            return subscription.Status;
        }

        Console.WriteLine($"[RedisCache] Cache MISS for {dealerId}. Not found in DB.");

        return null;
    }

    public async Task SetAsync(Guid dealerId, SubscriptionStatus status, TimeSpan ttl, CancellationToken ct = default)
    {
        var key = GetKey(dealerId);
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await _cache.SetStringAsync(key, status.ToString(), options, ct);

        // spr PR 3.1 (ADR B8, D2): anchor the PastDue grace window the FIRST time this cache observes
        // the status, and only then — a later SetAsync call while still PastDue (e.g. a Stripe dunning
        // retry re-fetched from the DB) must NOT push the anchor forward, or the grace period would never
        // actually expire. Any other status clears the anchor so a future PastDue starts a fresh window.
        var sinceKey = GetPastDueSinceKey(dealerId);
        if (status == SubscriptionStatus.PastDue)
        {
            var existing = await _cache.GetStringAsync(sinceKey, ct);
            if (string.IsNullOrEmpty(existing))
            {
                var sinceOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = PastDueSinceTtl };
                await _cache.SetStringAsync(sinceKey, DateTime.UtcNow.ToString("O"), sinceOptions, ct);
            }
        }
        else
        {
            await _cache.RemoveAsync(sinceKey, ct);
        }
    }

    public async Task InvalidateAsync(Guid dealerId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(GetKey(dealerId), ct);
    }

    public async Task<DateTime?> GetPastDueSinceAsync(Guid dealerId, CancellationToken ct = default)
    {
        var cached = await _cache.GetStringAsync(GetPastDueSinceKey(dealerId), ct);
        if (!string.IsNullOrEmpty(cached)
            && DateTime.TryParse(cached, null, System.Globalization.DateTimeStyles.RoundtripKind, out var since))
        {
            return since;
        }

        return null;
    }
}
