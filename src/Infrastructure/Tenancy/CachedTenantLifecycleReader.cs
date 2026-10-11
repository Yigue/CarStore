using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Tenancy;

internal sealed class CachedTenantLifecycleReader : ITenantLifecycleReader, ITenantLifecycleCache
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(30);

    private readonly ITenantLifecycleReader _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public CachedTenantLifecycleReader(ITenantLifecycleReader inner, IMemoryCache cache, TimeSpan? ttl = null)
    {
        _inner = inner;
        _cache = cache;
        _ttl = ttl ?? DefaultTtl;
    }

    public async Task<TenantLifecycleStatus> GetAsync(Guid dealerId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(Key(dealerId), out TenantLifecycleStatus cached))
            return cached;

        var status = await _inner.GetAsync(dealerId, ct);
        _cache.Set(Key(dealerId), status, _ttl);
        return status;
    }

    public void Invalidate(Guid dealerId) => _cache.Remove(Key(dealerId));

    private static string Key(Guid dealerId) => $"tenant-lifecycle:{dealerId:N}";
}
