using Infrastructure.Integrations;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace InfrastructureTests.Integrations;

/// <summary>
/// sfc C1.1 / ADR C22 (security review): the limiter never fails open and never burns attempts on its own outage.
/// A Redis timeout defers like any other Redis error; production with an enabled provider refuses the per-process
/// in-memory bucket, which would multiply the limit by the number of instances; nonsensical limits are rejected.
/// </summary>
public class RateLimiterResilienceTests
{
    [Fact]
    public async Task A_redis_timeout_defers_instead_of_escaping()
    {
        var database = new Mock<IDatabase>();
        database.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisTimeoutException("timeout", CommandStatus.Sent));
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        var limiter = new RedisRateLimiter(redis.Object, new RateLimiterOptions(), NullLogger<RedisRateLimiter>.Instance);

        var decision = await limiter.TryAcquireAsync(Guid.NewGuid(), "mercadolibre");

        decision.Allowed.Should().BeFalse();
        decision.RetryAfter.Should().BePositive();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public void Nonsensical_limits_are_rejected(int capacity, double refillPerSecond)
    {
        Action act = () => new RateLimiterOptions { Capacity = capacity, RefillPerSecond = refillPerSecond }.Validate();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Production_with_an_enabled_provider_requires_redis()
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Integrations:MercadoLibre:Enabled"] = "true",
            ["DataProtection:CertificateBase64"] = "present",
        }).Build();
        var guard = new DataProtectionProductionGuard(configuration, NullLogger<DataProtectionProductionGuard>.Instance);

        Action act = () => guard.Configure(_ => { });

        act.Should().Throw<InvalidOperationException>().WithMessage("*Redis*");
    }
}
