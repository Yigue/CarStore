using Application.Abstractions.Billing;
using Application.Billing.ApplyBillingEvent;
using Domain.Billing;
using Domain.Billing.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace InfrastructureTests.Billing;

/// <summary>
/// sfc A2.1 / RN-023: the domain logic of every normalized <see cref="BillingEvent"/> lives in
/// <see cref="ApplyBillingEventCommandHandler"/>. A state event older than the last applied one is a
/// logged no-op; non-state events (refund, trial notice) are applied regardless of order.
/// </summary>
public class ApplyBillingEventCommandHandlerTests
{
    private static readonly DateTime T1 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = T1.AddMinutes(5);
    private static readonly DateTime T0 = T1.AddMinutes(-5);

    private readonly Mock<IDealerSubscriptionRepository> _repo = new();
    private readonly CapturingLogger _logger = new();
    private readonly ApplyBillingEventCommandHandler _handler;

    public ApplyBillingEventCommandHandlerTests()
    {
        _handler = new ApplyBillingEventCommandHandler(_repo.Object, _logger);
    }

    private DealerSubscription ActiveSubscription(DateTime lastEventAt)
    {
        var sub = DealerSubscription.Create(Guid.NewGuid(), "cus_1", "sub_1", "price_1");
        sub.Activate("sub_1", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub.TryRegisterProviderEvent(lastEventAt).Should().BeTrue();
        sub.ClearDomainEvents();
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_1", It.IsAny<CancellationToken>())).ReturnsAsync(sub);
        return sub;
    }

    private static SubscriptionChanged Canceled(DateTime at, string id = "evt_c") =>
        new(id, at, "cus_1", "sub_1", "canceled", null, null, null, string.Empty, null);

    // --- RN-023: out-of-order state events ---

    [Fact]
    public async Task StateEvent_OlderThanLastApplied_IsNoOpWithLog()
    {
        var sub = ActiveSubscription(T2);

        var result = await _handler.Handle(new ApplyBillingEventCommand(Canceled(T1)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.LastProviderEventAtUtc.Should().Be(T2);
        sub.DomainEvents.Should().BeEmpty();
        _repo.Verify(r => r.Update(It.IsAny<DealerSubscription>()), Times.Never);
        _logger.EventNames.Should().Contain("BillingEventOutOfOrder");
    }

    [Fact]
    public async Task StateEvent_NewerThanLastApplied_IsAppliedAndAdvancesReference()
    {
        var sub = ActiveSubscription(T1);

        var result = await _handler.Handle(new ApplyBillingEventCommand(Canceled(T2)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.LastProviderEventAtUtc.Should().Be(T2);
        _repo.Verify(r => r.Update(sub), Times.Once);
        _logger.EventNames.Should().NotContain("BillingEventOutOfOrder");
    }

    [Fact]
    public async Task StateEvent_WithSameTimestampAsLastApplied_IsApplied()
    {
        var sub = ActiveSubscription(T1);

        await _handler.Handle(new ApplyBillingEventCommand(Canceled(T1)), CancellationToken.None);

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task PaymentFailed_OlderThanLastApplied_IsNoOp()
    {
        var sub = ActiveSubscription(T2);

        var result = await _handler.Handle(
            new ApplyBillingEventCommand(new PaymentFailed("evt_f", T1, "cus_1", "sub_1", 1)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sub.Status.Should().Be(SubscriptionStatus.Active);
        _logger.EventNames.Should().Contain("BillingEventOutOfOrder");
    }

    [Fact]
    public async Task PaymentSucceeded_OlderThanLastApplied_IsNoOp()
    {
        var sub = ActiveSubscription(T2);
        sub.MarkPastDue();
        sub.ClearDomainEvents();

        await _handler.Handle(new ApplyBillingEventCommand(
            new PaymentSucceeded("evt_s", T1, "cus_1", "sub_1", T1, T1.AddMonths(1))), CancellationToken.None);

        sub.Status.Should().Be(SubscriptionStatus.PastDue);
        _logger.EventNames.Should().Contain("BillingEventOutOfOrder");
    }

    [Fact]
    public async Task OutOfOrder_AfterAnotherStateEvent_DoesNotMoveReferenceBackwards()
    {
        var sub = ActiveSubscription(T1);

        await _handler.Handle(new ApplyBillingEventCommand(
            new PaymentFailed("evt_f", T2, "cus_1", "sub_1", 1)), CancellationToken.None);
        await _handler.Handle(new ApplyBillingEventCommand(
            new PaymentSucceeded("evt_s", T0, "cus_1", "sub_1", T0, T0.AddMonths(1))), CancellationToken.None);

        sub.Status.Should().Be(SubscriptionStatus.PastDue);
        sub.LastProviderEventAtUtc.Should().Be(T2);
    }

    // --- non-state events are applied even when "old" ---

    [Fact]
    public async Task ChargeRefunded_OlderThanLastApplied_IsStillApplied()
    {
        var sub = ActiveSubscription(T2);

        var result = await _handler.Handle(new ApplyBillingEventCommand(
            new ChargeRefunded("evt_r", T1, "cus_1", "ch_1", "in_1", 1000, 1000, "usd", null)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _logger.EventNames.Should().Contain("BillingEventApplied");
        _logger.EventNames.Should().NotContain("BillingEventOutOfOrder");
        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.LastProviderEventAtUtc.Should().Be(T2);
    }

    [Fact]
    public async Task TrialWillEnd_OlderThanLastApplied_IsStillApplied()
    {
        var sub = ActiveSubscription(T2);

        var result = await _handler.Handle(new ApplyBillingEventCommand(
            new TrialWillEnd("evt_t", T1, "cus_1", "sub_1", T1.AddDays(3))), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _logger.EventNames.Should().Contain("BillingEventApplied");
        _logger.EventNames.Should().NotContain("BillingEventOutOfOrder");
        sub.LastProviderEventAtUtc.Should().Be(T2);
    }

    [Fact]
    public async Task StateEvent_ForUnknownCustomer_IsIgnoredWithoutError()
    {
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_x", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);

        var result = await _handler.Handle(new ApplyBillingEventCommand(
            new PaymentFailed("evt_f", T1, "cus_x", null, 1)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.Update(It.IsAny<DealerSubscription>()), Times.Never);
    }

    // --- logic moved from the old StripeWebhookProcessor / HandleStripeWebhook handler ---

    [Fact]
    public async Task SubscriptionDeleted_CancelsInsteadOfSuspending_AndRaisesCancelledEvent()
    {
        var sub = ActiveSubscription(T1);

        await _handler.Handle(new ApplyBillingEventCommand(Canceled(T2)), CancellationToken.None);

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.CancelledAt.Should().NotBeNull();
        var ev = sub.DomainEvents.OfType<SubscriptionCancelledDomainEvent>().Should().ContainSingle().Subject;
        ev.CancelledAtUtc.Should().Be(sub.CancelledAt!.Value);
        sub.DomainEvents.OfType<SubscriptionSuspendedDomainEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task SubscriptionDeleted_OnAlreadyCancelled_DoesNotThrowNorRaiseAgain()
    {
        var sub = ActiveSubscription(T1);
        sub.Cancel();
        sub.ClearDomainEvents();

        var result = await _handler.Handle(new ApplyBillingEventCommand(Canceled(T2)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sub.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SubscriptionDeleted_OnSuspendedSubscription_Cancels()
    {
        var sub = ActiveSubscription(T1);
        sub.Suspend();

        await _handler.Handle(new ApplyBillingEventCommand(Canceled(T2)), CancellationToken.None);

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task PaymentFailed_SecondFailureWhilePastDue_Suspends()
    {
        var sub = ActiveSubscription(T0);
        sub.MarkPastDue();

        await _handler.Handle(new ApplyBillingEventCommand(
            new PaymentFailed("evt_f", T1, "cus_1", "sub_1", 2)), CancellationToken.None);

        sub.Status.Should().Be(SubscriptionStatus.Suspended);
    }

    [Fact]
    public async Task SubscriptionChanged_ForUnknownCustomerWithDealerMetadata_CreatesSubscriptionWithReference()
    {
        var dealerId = Guid.NewGuid();
        _repo.Setup(r => r.GetByStripeCustomerIdAsync("cus_new", It.IsAny<CancellationToken>()))
            .ReturnsAsync((DealerSubscription?)null);
        DealerSubscription? added = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<DealerSubscription>(), It.IsAny<CancellationToken>()))
            .Callback<DealerSubscription, CancellationToken>((s, _) => added = s)
            .Returns(Task.CompletedTask);

        await _handler.Handle(new ApplyBillingEventCommand(new SubscriptionChanged(
            "evt_n", T1, "cus_new", "sub_n", "trialing", T1, T1.AddDays(14), T1.AddDays(14), "price_1", dealerId)),
            CancellationToken.None);

        added.Should().NotBeNull();
        added!.DealerId.Should().Be(dealerId);
        added.Status.Should().Be(SubscriptionStatus.Trialing);
        added.LastProviderEventAtUtc.Should().Be(T1);
    }

    private sealed class CapturingLogger : ILogger<ApplyBillingEventCommandHandler>
    {
        public List<string> EventNames { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (eventId.Name is not null)
            {
                EventNames.Add(eventId.Name);
            }
        }
    }
}
