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

    private static string GetKey(Guid dealerId) => $"subscription:status:{dealerId}";

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
    }

    public async Task InvalidateAsync(Guid dealerId, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(GetKey(dealerId), ct);
    }
}
