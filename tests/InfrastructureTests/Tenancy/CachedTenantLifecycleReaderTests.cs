using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using FluentAssertions;
using Infrastructure.Tenancy;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace InfrastructureTests.Tenancy;

public class CachedTenantLifecycleReaderTests
{
    private sealed class CountingReader(TenantLifecycleStatus initial) : ITenantLifecycleReader
    {
        public int Calls { get; private set; }
        public TenantLifecycleStatus Current { get; set; } = initial;

        public Task<TenantLifecycleStatus> GetAsync(Guid dealerId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Current);
        }
    }

    private static CachedTenantLifecycleReader Create(ITenantLifecycleReader inner, IMemoryCache? cache = null) =>
        new(inner, cache ?? new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task SecondCallWithinTtl_DoesNotHitInnerReader()
    {
        var inner = new CountingReader(TenantLifecycleStatus.CancelledGrace);
        var sut = Create(inner);
        var dealerId = Guid.NewGuid();

        var first = await sut.GetAsync(dealerId);
        var second = await sut.GetAsync(dealerId);

        first.Should().Be(TenantLifecycleStatus.CancelledGrace);
        second.Should().Be(TenantLifecycleStatus.CancelledGrace);
        inner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task DifferentDealers_AreCachedIndependently()
    {
        var inner = new CountingReader(TenantLifecycleStatus.Active);
        var sut = Create(inner);

        await sut.GetAsync(Guid.NewGuid());
        await sut.GetAsync(Guid.NewGuid());

        inner.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Invalidate_MakesNextCallSeeTheNewLifecycle()
    {
        var inner = new CountingReader(TenantLifecycleStatus.Active);
        var sut = Create(inner);
        var dealerId = Guid.NewGuid();
        (await sut.GetAsync(dealerId)).Should().Be(TenantLifecycleStatus.Active);

        inner.Current = TenantLifecycleStatus.CancelledGrace;
        ((ITenantLifecycleCache)sut).Invalidate(dealerId);

        (await sut.GetAsync(dealerId)).Should().Be(TenantLifecycleStatus.CancelledGrace);
        inner.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Invalidate_ThroughAnotherInstanceSharingTheMemoryCache_Works()
    {
        // Scoped instances share the singleton IMemoryCache; the handler's instance is not the guard's.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var inner = new CountingReader(TenantLifecycleStatus.Active);
        var guardSide = Create(inner, cache);
        var handlerSide = Create(inner, cache);
        var dealerId = Guid.NewGuid();
        await guardSide.GetAsync(dealerId);

        inner.Current = TenantLifecycleStatus.Purged;
        ((ITenantLifecycleCache)handlerSide).Invalidate(dealerId);

        (await guardSide.GetAsync(dealerId)).Should().Be(TenantLifecycleStatus.Purged);
    }

    [Fact]
    public async Task DealerWithoutSettingsRow_DefaultsToActive_AndIsCached()
    {
        // EfTenantLifecycleReader returns default(enum) when no row exists.
        var inner = new CountingReader(default);
        var sut = Create(inner);
        var dealerId = Guid.NewGuid();

        var act = async () => await sut.GetAsync(dealerId);

        (await act.Should().NotThrowAsync()).Subject.Should().Be(TenantLifecycleStatus.Active);
        await sut.GetAsync(dealerId);
        inner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task EntryExpiresAfterTtl()
    {
        var inner = new CountingReader(TenantLifecycleStatus.Active);
        var sut = new CachedTenantLifecycleReader(inner, new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMilliseconds(50));
        var dealerId = Guid.NewGuid();
        await sut.GetAsync(dealerId);

        await Task.Delay(200);
        await sut.GetAsync(dealerId);

        inner.Calls.Should().Be(2);
    }
}
