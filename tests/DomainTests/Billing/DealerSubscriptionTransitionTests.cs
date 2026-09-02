using Domain.Billing;
using Domain.Billing.Events;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace DomainTests.Billing;

public class DealerSubscriptionTransitionTests
{
    private static DealerSubscription CreateSubscription(Guid dealerId)
    {
        return DealerSubscription.Create(dealerId, "cus_123", "sub_123", "plan_123");
    }

    [Fact]
    public void Create_ShouldStartInTrialing()
    {
        var dealerId = Guid.NewGuid();
        var sub = CreateSubscription(dealerId);

        sub.Status.Should().Be(SubscriptionStatus.Trialing);
        sub.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromDays(15)); // Trial Ends At is in the future
        sub.DealerId.Should().Be(dealerId);
        sub.PlanId.Should().Be("plan_123");
    }

    [Fact]
    public void Activate_FromTrialing_ShouldChangeStatusToActiveAndRaiseEvent()
    {
        var sub = CreateSubscription(Guid.NewGuid());
        var start = DateTime.UtcNow;
        var end = start.AddMonths(1);

        sub.Activate("sub_active", start, end);

        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.StripeSubscriptionId.Should().Be("sub_active");
        sub.CurrentPeriodStart.Should().Be(start);
        sub.CurrentPeriodEnd.Should().Be(end);

        var ev = sub.DomainEvents.OfType<SubscriptionActivatedDomainEvent>().Should().ContainSingle().Subject;
        ev.SubscriptionId.Should().Be(sub.Id);
        ev.DealerId.Should().Be(sub.DealerId);
        ev.StripeSubscriptionId.Should().Be("sub_active");
    }

    [Fact]
    public void MarkPastDue_FromActive_ShouldChangeStatusToPastDueAndRaiseEvent()
    {
        var sub = CreateSubscription(Guid.NewGuid());
        sub.Activate("sub_active", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub.ClearDomainEvents();

        sub.MarkPastDue();

        sub.Status.Should().Be(SubscriptionStatus.PastDue);
        var ev = sub.DomainEvents.OfType<SubscriptionPaymentFailedDomainEvent>().Should().ContainSingle().Subject;
        ev.SubscriptionId.Should().Be(sub.Id);
        ev.DealerId.Should().Be(sub.DealerId);
    }

    [Fact]
    public void Suspend_FromActiveOrPastDue_ShouldChangeStatusToSuspendedAndRaiseEvent()
    {
        // From Active
        var sub1 = CreateSubscription(Guid.NewGuid());
        sub1.Activate("sub_active", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub1.ClearDomainEvents();

        sub1.Suspend();

        sub1.Status.Should().Be(SubscriptionStatus.Suspended);
        sub1.DomainEvents.OfType<SubscriptionSuspendedDomainEvent>().Should().ContainSingle();

        // From PastDue
        var sub2 = CreateSubscription(Guid.NewGuid());
        sub2.Activate("sub_active", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub2.MarkPastDue();
        sub2.ClearDomainEvents();

        sub2.Suspend();

        sub2.Status.Should().Be(SubscriptionStatus.Suspended);
        sub2.DomainEvents.OfType<SubscriptionSuspendedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Cancel_FromAnyNonCancelledState_ShouldChangeStatusToCancelledAndRaiseEvent()
    {
        var sub = CreateSubscription(Guid.NewGuid());
        sub.Activate("sub_active", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub.ClearDomainEvents();

        sub.Cancel();

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        var ev = sub.DomainEvents.OfType<SubscriptionCancelledDomainEvent>().Should().ContainSingle().Subject;
        ev.SubscriptionId.Should().Be(sub.Id);
        ev.DealerId.Should().Be(sub.DealerId);
    }

    [Fact]
    public void Cancelled_IsTerminal_AnyTransitionThrows()
    {
        var sub = CreateSubscription(Guid.NewGuid());
        sub.Cancel();

        // Activate
        var act1 = () => sub.Activate("sub_active", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        act1.Should().Throw<InvalidSubscriptionTransitionException>();

        // MarkPastDue
        var act2 = () => sub.MarkPastDue();
        act2.Should().Throw<InvalidSubscriptionTransitionException>();

        // Suspend
        var act3 = () => sub.Suspend();
        act3.Should().Throw<InvalidSubscriptionTransitionException>();

        // Cancel
        var act4 = () => sub.Cancel();
        act4.Should().Throw<InvalidSubscriptionTransitionException>();

        // RenewPeriod
        var act5 = () => sub.RenewPeriod(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        act5.Should().Throw<InvalidSubscriptionTransitionException>();
    }

    [Fact]
    public void IllegalTransitions_ShouldThrowException()
    {
        // Try to MarkPastDue from Trialing
        var sub = CreateSubscription(Guid.NewGuid());
        var act1 = () => sub.MarkPastDue();
        act1.Should().Throw<InvalidSubscriptionTransitionException>();

        // Try to MarkPastDue from Cancelled
        sub.Cancel();
        var act2 = () => sub.MarkPastDue();
        act2.Should().Throw<InvalidSubscriptionTransitionException>();
    }
}
