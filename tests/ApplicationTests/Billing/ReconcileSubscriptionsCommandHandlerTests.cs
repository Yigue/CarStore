using Application.Abstractions.Billing;
using Application.Billing.Commands.ReconcileSubscriptions;
using Domain.Billing;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel;
using Xunit;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace ApplicationTests.Billing;

/// <summary>
/// spr ADR B9 (tasks 3.2.1/3.2.2): flags (never deletes) dealers without a confirmed subscription once
/// they are past the grace window, heals them first when Stripe already has one, and alerts once.
/// </summary>
public class ReconcileSubscriptionsCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Grace = TimeSpan.FromHours(48);

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime UtcNow => Now;
    }

    private sealed class CapturingLogger : ILogger<ReconcileSubscriptionsCommandHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private readonly Application.UnitTests.TestApplicationDbContext _context;
    private readonly Mock<ISubscriptionGateway> _gateway = new();
    private readonly CapturingLogger _logger = new();

    public ReconcileSubscriptionsCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<Application.UnitTests.TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new Application.UnitTests.TestApplicationDbContext(options);
        _gateway.Setup(g => g.FindSubscriptionForDealerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalSubscription?)null);
    }

    private ReconcileSubscriptionsCommandHandler Handler() =>
        new(_context, _gateway.Object, new FixedClock(), _logger);

    private async Task<Guid> SeedDealerAsync(DateTime createdAtUtc, bool withIncompleteRow, bool backdateRow = true)
    {
        var dealerId = Guid.NewGuid();
        var settings = new DealerSettingsEntity(dealerId, dealerId, "Dealer", "d@x.com", true, $"d{dealerId:N}"[..12]);
        typeof(DealerSettingsEntity).GetProperty(nameof(DealerSettingsEntity.CreatedAt))!.SetValue(settings, createdAtUtc);
        _context.DealerSettings.Add(settings);
        if (withIncompleteRow)
        {
            var row = DealerSubscription.CreatePending(dealerId, null, string.Empty);
            if (backdateRow)
            {
                typeof(DealerSubscription).GetProperty(nameof(DealerSubscription.CurrentPeriodStart))!
                    .SetValue(row, createdAtUtc);
            }
            _context.DealerSubscriptions.Add(row);
        }
        await _context.SaveChangesAsync();
        return dealerId;
    }

    private Task<Result<ReconcileSubscriptionsResult>> Run() =>
        Handler().Handle(new ReconcileSubscriptionsCommand(Grace, 50), CancellationToken.None);

    [Fact]
    public async Task AbandonedIncompleteRow_PastGrace_AndNotInStripe_IsFlagged_AndAlertedOnce()
    {
        var dealerId = await SeedDealerAsync(Now.AddDays(-5), withIncompleteRow: true);

        var result = await Run();

        result.IsSuccess.Should().BeTrue();
        result.Value.Flagged.Should().Be(1);
        var flag = await _context.BillingReconciliationFlags.SingleAsync(f => f.DealerId == dealerId);
        flag.IsOpen.Should().BeTrue();
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains(dealerId.ToString()));
    }

    [Fact]
    public async Task SecondRun_DoesNotDuplicateTheAlertNorTheFlag()
    {
        var dealerId = await SeedDealerAsync(Now.AddDays(-5), withIncompleteRow: true);

        await Run();
        var second = await Run();

        second.Value.Flagged.Should().Be(0);
        (await _context.BillingReconciliationFlags.CountAsync(f => f.DealerId == dealerId)).Should().Be(1);
        _logger.Entries.Count(e => e.Level == LogLevel.Error && e.Message.Contains(dealerId.ToString())).Should().Be(1);
    }

    [Fact]
    public async Task IncompleteRow_WithinGrace_IsNotFlagged()
    {
        var dealerId = await SeedDealerAsync(Now.AddHours(-3), withIncompleteRow: true);

        var result = await Run();

        result.Value.Flagged.Should().Be(0);
        (await _context.BillingReconciliationFlags.AnyAsync(f => f.DealerId == dealerId)).Should().BeFalse();
    }

    [Fact]
    public async Task LegacyDealerWithoutAnyRow_IsTreatedLikeAnAbandonedIncompleteRow()
    {
        var dealerId = await SeedDealerAsync(Now.AddDays(-30), withIncompleteRow: false);

        var result = await Run();

        result.Value.Flagged.Should().Be(1);
        (await _context.BillingReconciliationFlags.SingleAsync(f => f.DealerId == dealerId)).IsOpen.Should().BeTrue();
    }

    [Fact]
    public async Task WhenStripeAlreadyHasATrial_TheRowIsHealed_NotFlagged()
    {
        var dealerId = await SeedDealerAsync(Now.AddDays(-5), withIncompleteRow: true);
        _gateway.Setup(g => g.FindSubscriptionForDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalSubscription(
                "cus_9", "sub_9", SubscriptionStatus.Trialing, Now.AddDays(-4), Now.AddDays(10), Now.AddDays(10), "price_1"));

        var result = await Run();

        result.Value.Healed.Should().Be(1);
        result.Value.Flagged.Should().Be(0);
        var row = await _context.DealerSubscriptions.IgnoreQueryFilters().SingleAsync(s => s.DealerId == dealerId);
        row.Status.Should().Be(SubscriptionStatus.Trialing);
        row.StripeCustomerId.Should().Be("cus_9");
        row.StripeSubscriptionId.Should().Be("sub_9");
        (await _context.BillingReconciliationFlags.AnyAsync(f => f.DealerId == dealerId && f.ClearedAtUtc == null))
            .Should().BeFalse();
    }

    [Fact]
    public async Task LegacyDealerWithoutRow_WhenStripeHasActiveSubscription_GetsARowCreatedAndActivated()
    {
        var dealerId = await SeedDealerAsync(Now.AddDays(-30), withIncompleteRow: false);
        _gateway.Setup(g => g.FindSubscriptionForDealerAsync(dealerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalSubscription(
                "cus_9", "sub_9", SubscriptionStatus.Active, Now.AddDays(-4), Now.AddDays(26), null, "price_1"));

        await Run();

        var row = await _context.DealerSubscriptions.IgnoreQueryFilters().SingleAsync(s => s.DealerId == dealerId);
        row.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task OpenFlag_IsCleared_WhenTheRowBecomesConfirmed()
    {
        var dealerId = await SeedDealerAsync(Now.AddDays(-5), withIncompleteRow: true);
        await Run();
        var row = await _context.DealerSubscriptions.IgnoreQueryFilters().SingleAsync(s => s.DealerId == dealerId);
        row.Activate("sub_1", Now.AddDays(-1), Now.AddDays(29));
        await _context.SaveChangesAsync();

        var result = await Run();

        result.Value.Cleared.Should().Be(1);
        (await _context.BillingReconciliationFlags.SingleAsync(f => f.DealerId == dealerId)).ClearedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task NeverDeletesAnything()
    {
        var dealerId = await SeedDealerAsync(Now.AddDays(-5), withIncompleteRow: true);
        var dealersBefore = await _context.DealerSettings.IgnoreQueryFilters().CountAsync();
        var subsBefore = await _context.DealerSubscriptions.IgnoreQueryFilters().CountAsync();

        await Run();
        await Run();

        (await _context.DealerSettings.IgnoreQueryFilters().CountAsync()).Should().Be(dealersBefore);
        (await _context.DealerSubscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(subsBefore);
        (await _context.DealerSettings.IgnoreQueryFilters().AnyAsync(s => s.DealerId == dealerId)).Should().BeTrue();
    }

    [Fact]
    public async Task RespectsTheBatchSize()
    {
        for (var i = 0; i < 3; i++)
        {
            await SeedDealerAsync(Now.AddDays(-5), withIncompleteRow: true);
        }

        var result = await Handler().Handle(new ReconcileSubscriptionsCommand(Grace, 2), CancellationToken.None);

        result.Value.Flagged.Should().Be(2);
    }
}
