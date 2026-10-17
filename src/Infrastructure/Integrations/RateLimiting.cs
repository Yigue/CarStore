using System.Collections.Concurrent;
using Application.Abstractions.Integrations;
using Microsoft.Extensions.Logging;
using SharedKernel;
using StackExchange.Redis;

namespace Infrastructure.Integrations;

/// <summary>sfc C1.1 / ADR C22: bound from <c>Integrations:RateLimit</c>; one bucket per (dealer, provider).</summary>
public sealed class RateLimiterOptions
{
    public const string SectionName = "Integrations:RateLimit";

    public int Capacity { get; set; } = 10;
    public double RefillPerSecond { get; set; } = 1;
}

/// <summary>Single-process token bucket for dev/tests (no Redis configured).</summary>
public sealed class InMemoryRateLimiter(IDateTimeProvider clock, RateLimiterOptions options) : IRateLimiter
{
    private sealed class Bucket
    {
        public double Tokens;
        public DateTime LastUtc;
    }

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();

    public Task<RateLimitDecision> TryAcquireAsync(Guid dealerId, string provider, CancellationToken cancellationToken = default)
    {
        DateTime now = clock.UtcNow;
        Bucket bucket = _buckets.GetOrAdd($"{dealerId:N}:{provider}", _ => new Bucket { Tokens = options.Capacity, LastUtc = now });

        lock (bucket)
        {
            double elapsed = Math.Max(0, (now - bucket.LastUtc).TotalSeconds);
            bucket.Tokens = Math.Min(options.Capacity, bucket.Tokens + elapsed * options.RefillPerSecond);
            bucket.LastUtc = now;

            if (bucket.Tokens >= 1)
            {
                bucket.Tokens -= 1;
                return Task.FromResult(new RateLimitDecision(true, TimeSpan.Zero));
            }

            return Task.FromResult(new RateLimitDecision(false, TimeSpan.FromSeconds((1 - bucket.Tokens) / options.RefillPerSecond)));
        }
    }
}

/// <summary>Token bucket shared across instances; refill and take are one atomic Lua script (Redis TIME as the clock).</summary>
public sealed class RedisRateLimiter(
    IConnectionMultiplexer redis, RateLimiterOptions options, ILogger<RedisRateLimiter> logger) : IRateLimiter
{
    private static readonly TimeSpan RedisDownRetry = TimeSpan.FromSeconds(30);

    private const string Script = """
        local capacity = tonumber(ARGV[1])
        local rate = tonumber(ARGV[2])
        local t = redis.call('TIME')
        local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
        local data = redis.call('HMGET', KEYS[1], 'tokens', 'ts')
        local tokens = tonumber(data[1])
        local ts = tonumber(data[2])
        if tokens == nil then
          tokens = capacity
          ts = now
        end
        tokens = math.min(capacity, tokens + math.max(0, now - ts) * rate / 1000)
        local allowed = 0
        local wait = 0
        if tokens >= 1 then
          tokens = tokens - 1
          allowed = 1
        else
          wait = math.ceil((1 - tokens) * 1000 / rate)
        end
        redis.call('HSET', KEYS[1], 'tokens', tostring(tokens), 'ts', tostring(now))
        redis.call('PEXPIRE', KEYS[1], math.ceil(capacity / rate * 1000) * 2)
        return { allowed, wait }
        """;

    public async Task<RateLimitDecision> TryAcquireAsync(Guid dealerId, string provider, CancellationToken cancellationToken = default)
    {
        try
        {
            RedisResult result = await redis.GetDatabase().ScriptEvaluateAsync(
                Script,
                [(RedisKey)$"CarStore:ratelimit:{dealerId:N}:{provider}"],
                [options.Capacity, options.RefillPerSecond]);

            var values = (long[])result!;
            return values[0] == 1
                ? new RateLimitDecision(true, TimeSpan.Zero)
                : new RateLimitDecision(false, TimeSpan.FromMilliseconds(values[1]));
        }
        catch (RedisException ex)
        {
            // Fail closed: nothing is sent while the limiter is unreachable, and the command keeps its attempts.
            logger.LogWarning(ex, "Rate limiter unavailable for {Provider}; deferring", provider);
            return new RateLimitDecision(false, RedisDownRetry);
        }
    }
}
