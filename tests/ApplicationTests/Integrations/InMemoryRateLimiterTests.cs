using FluentAssertions;
using Infrastructure.Integrations;

namespace Application.UnitTests.Integrations;

/// <summary>sfc C1.1 / ADR C22: token bucket per (dealer, provider).</summary>
public class InMemoryRateLimiterTests
{
    private static readonly Guid DealerA = Guid.NewGuid();
    private static readonly Guid DealerB = Guid.NewGuid();

    private static (InMemoryRateLimiter Limiter, FakeDateTimeProvider Clock) Create(int capacity = 3, double refillPerSecond = 1)
    {
        var clock = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc) };
        return (new InMemoryRateLimiter(clock, new RateLimiterOptions { Capacity = capacity, RefillPerSecond = refillPerSecond }), clock);
    }

    [Fact]
    public async Task Allows_UpToCapacity_ThenDenies_WithRetryAfter()
    {
        var (limiter, _) = Create(capacity: 3, refillPerSecond: 1);

        for (int i = 0; i < 3; i++)
        {
            (await limiter.TryAcquireAsync(DealerA, "mercadolibre")).Allowed.Should().BeTrue();
        }

        var denied = await limiter.TryAcquireAsync(DealerA, "mercadolibre");
        denied.Allowed.Should().BeFalse();
        denied.RetryAfter.Should().BeCloseTo(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public async Task Refills_OverTime_UpToCapacity()
    {
        var (limiter, clock) = Create(capacity: 2, refillPerSecond: 1);
        await limiter.TryAcquireAsync(DealerA, "meta");
        await limiter.TryAcquireAsync(DealerA, "meta");
        (await limiter.TryAcquireAsync(DealerA, "meta")).Allowed.Should().BeFalse();

        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        (await limiter.TryAcquireAsync(DealerA, "meta")).Allowed.Should().BeTrue();
        (await limiter.TryAcquireAsync(DealerA, "meta")).Allowed.Should().BeFalse();

        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        (await limiter.TryAcquireAsync(DealerA, "meta")).Allowed.Should().BeTrue();
        (await limiter.TryAcquireAsync(DealerA, "meta")).Allowed.Should().BeTrue();
        (await limiter.TryAcquireAsync(DealerA, "meta")).Allowed.Should().BeFalse(); // capped at capacity
    }

    [Fact]
    public async Task Buckets_AreIsolated_PerDealerAndProvider()
    {
        var (limiter, _) = Create(capacity: 1);
        (await limiter.TryAcquireAsync(DealerA, "meta")).Allowed.Should().BeTrue();

        (await limiter.TryAcquireAsync(DealerA, "meta")).Allowed.Should().BeFalse();
        (await limiter.TryAcquireAsync(DealerB, "meta")).Allowed.Should().BeTrue();
        (await limiter.TryAcquireAsync(DealerA, "mercadolibre")).Allowed.Should().BeTrue();
    }
}
