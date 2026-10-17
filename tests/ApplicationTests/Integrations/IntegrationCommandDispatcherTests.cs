using Application.Abstractions.Integrations;
using Domain.Integrations;
using FluentAssertions;
using Infrastructure.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Integrations;

/// <summary>sfc C1.1 / ADR C22: queue drain semantics (limiter, backoff, dead letter, enable flag).</summary>
public class IntegrationCommandDispatcherTests
{
    private static readonly Guid Dealer = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);

    private sealed class FakeClient(string provider, Func<IntegrationRequest, IntegrationResult> responder) : IIntegrationClient
    {
        public int Calls { get; private set; }
        public string Provider => provider;

        public Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class Settings(params string[] enabled) : IIntegrationProviderSettings
    {
        public bool IsEnabled(string provider) => enabled.Contains(provider);
    }

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IntegrationCommandDispatcher CreateDispatcher(
        TestApplicationDbContext ctx, IRateLimiter limiter, IEnumerable<IIntegrationClient> clients, params string[] enabled) =>
        new(ctx, limiter, clients, new Settings(enabled), new FakeDateTimeProvider { UtcNow = Now },
            NullLogger<IntegrationCommandDispatcher>.Instance);

    private static IRateLimiter Allow()
    {
        var limiter = new Mock<IRateLimiter>();
        limiter.Setup(l => l.TryAcquireAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, TimeSpan.Zero));
        return limiter.Object;
    }

    private static async Task<IntegrationCommand> SeedAsync(TestApplicationDbContext ctx, string provider = "meta", int failedAttempts = 0)
    {
        var command = IntegrationCommand.Enqueue(Dealer, provider, "publish", "{}", Now.AddMinutes(-1));
        for (int i = 0; i < failedAttempts; i++)
        {
            command.RecordFailure(Now.AddMinutes(-1), "boom");
            command.Reschedule(Now.AddMinutes(-1));
        }

        ctx.IntegrationCommands.Add(command);
        await ctx.SaveChangesAsync();
        return command;
    }

    [Fact]
    public async Task Success_MarksDone()
    {
        using var ctx = CreateContext();
        var command = await SeedAsync(ctx);
        var client = new FakeClient("meta", _ => IntegrationResult.Success());

        await CreateDispatcher(ctx, Allow(), [client], "meta").DispatchDueAsync(default);

        command.Status.Should().Be(IntegrationCommandStatus.Done);
        client.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Failure_IncrementsAttempts_WithExponentialBackoff()
    {
        using var ctx = CreateContext();
        var command = await SeedAsync(ctx);
        var client = new FakeClient("meta", _ => IntegrationResult.Failure("provider 500"));

        await CreateDispatcher(ctx, Allow(), [client], "meta").DispatchDueAsync(default);

        command.Status.Should().Be(IntegrationCommandStatus.Pending);
        command.Attempts.Should().Be(1);
        command.LastError.Should().Be("provider 500");
        command.NextAttemptAtUtc.Should().Be(Now + IntegrationCommand.GetBackoff(1));
        IntegrationCommand.GetBackoff(2).Should().Be(IntegrationCommand.GetBackoff(1) * 2);
    }

    [Fact]
    public async Task Failure_AtMaxAttempts_GoesToDeadLetter()
    {
        using var ctx = CreateContext();
        var command = await SeedAsync(ctx, failedAttempts: IntegrationCommand.MaxAttempts - 1);
        var client = new FakeClient("meta", _ => IntegrationResult.Failure("still failing"));

        await CreateDispatcher(ctx, Allow(), [client], "meta").DispatchDueAsync(default);

        command.Status.Should().Be(IntegrationCommandStatus.DeadLetter);
        command.Attempts.Should().Be(IntegrationCommand.MaxAttempts);
    }

    [Fact]
    public async Task RateLimited_Reschedules_WithoutConsumingAnAttempt_AndWithoutCallingTheClient()
    {
        using var ctx = CreateContext();
        var command = await SeedAsync(ctx);
        var limiter = new Mock<IRateLimiter>();
        limiter.Setup(l => l.TryAcquireAsync(Dealer, "meta", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(false, TimeSpan.FromSeconds(7)));
        var client = new FakeClient("meta", _ => IntegrationResult.Success());

        await CreateDispatcher(ctx, limiter.Object, [client], "meta").DispatchDueAsync(default);

        command.Status.Should().Be(IntegrationCommandStatus.Pending);
        command.Attempts.Should().Be(0);
        command.NextAttemptAtUtc.Should().Be(Now.AddSeconds(7));
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task DisabledProvider_IsSkipped_AndStaysPending()
    {
        using var ctx = CreateContext();
        var command = await SeedAsync(ctx);
        var client = new FakeClient("meta", _ => IntegrationResult.Success());

        await CreateDispatcher(ctx, Allow(), [client] /* nothing enabled */).DispatchDueAsync(default);

        command.Status.Should().Be(IntegrationCommandStatus.Pending);
        command.Attempts.Should().Be(0);
        client.Calls.Should().Be(0);
    }

    [Fact]
    public async Task UnknownProvider_DeadLettersWithAClearError()
    {
        using var ctx = CreateContext();
        var command = await SeedAsync(ctx, provider: "tiktok");

        await CreateDispatcher(ctx, Allow(), [], "tiktok").DispatchDueAsync(default);

        command.Status.Should().Be(IntegrationCommandStatus.DeadLetter);
        command.LastError.Should().Contain("tiktok").And.Contain("No integration client");
    }

    [Fact]
    public async Task ClientThrowing_IsTreatedAsARetryableFailure()
    {
        using var ctx = CreateContext();
        var command = await SeedAsync(ctx);
        var client = new FakeClient("meta", _ => throw new HttpRequestException("socket closed"));

        await CreateDispatcher(ctx, Allow(), [client], "meta").DispatchDueAsync(default);

        command.Attempts.Should().Be(1);
        command.LastError.Should().Contain("socket closed");
    }

    [Fact]
    public async Task FutureCommands_AreNotPickedUp()
    {
        using var ctx = CreateContext();
        var command = IntegrationCommand.Enqueue(Dealer, "meta", "publish", "{}", Now);
        command.Reschedule(Now.AddMinutes(5));
        ctx.IntegrationCommands.Add(command);
        await ctx.SaveChangesAsync();
        var client = new FakeClient("meta", _ => IntegrationResult.Success());

        await CreateDispatcher(ctx, Allow(), [client], "meta").DispatchDueAsync(default);

        client.Calls.Should().Be(0);
    }
}
