using Domain.Billing;
using Domain.Billing.Events;
using FluentAssertions;

namespace DomainTests.Billing;

/// <summary>
/// spr ADR B8: provisioning always leaves an <see cref="SubscriptionStatus.Incomplete"/> row, so a
/// dealer never exists without a subscription. These pin how that row moves once Stripe reports.
/// </summary>
public class DealerSubscriptionIncompleteTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddDays(30);

    [Fact]
    public void CreatePending_ShouldBeIncomplete_WithOptionalCustomer()
    {
        var dealerId = Guid.NewGuid();

        var withCustomer = DealerSubscription.CreatePending(dealerId, "cus_1", "price_1");
        var withoutCustomer = DealerSubscription.CreatePending(dealerId, null, string.Empty);

        withCustomer.Status.Should().Be(SubscriptionStatus.Incomplete);
        withCustomer.DealerId.Should().Be(dealerId);
        withCustomer.StripeCustomerId.Should().Be("cus_1");
        withCustomer.StripeSubscriptionId.Should().BeNull();
        withCustomer.TrialEndsAt.Should().BeNull();
        withoutCustomer.Status.Should().Be(SubscriptionStatus.Incomplete);
        withoutCustomer.StripeCustomerId.Should().BeNull();
    }

    [Fact]
    public void StartTrial_FromIncomplete_ShouldBecomeTrialing_AndLinkStripe()
    {
        var sub = DealerSubscription.CreatePending(Guid.NewGuid(), "cus_1", "price_1");

        sub.StartTrial("sub_1", End, Start, End);

        sub.Status.Should().Be(SubscriptionStatus.Trialing);
        sub.StripeSubscriptionId.Should().Be("sub_1");
        sub.TrialEndsAt.Should().Be(End);
        sub.DomainEvents.Should().ContainSingle(e => e is SubscriptionActivatedDomainEvent,
            "the status cache must be invalidated when a dealer leaves Incomplete");
    }

    [Fact]
    public void Activate_FromIncomplete_ShouldBecomeActive()
    {
        var sub = DealerSubscription.CreatePending(Guid.NewGuid(), "cus_1", "price_1");

        sub.Activate("sub_1", Start, End);

        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.StripeSubscriptionId.Should().Be("sub_1");
    }

    [Fact]
    public void MarkPastDue_FromIncomplete_ShouldStayIncomplete()
    {
        // Stripe "incomplete" = first payment not confirmed yet. That is not a late renewal.
        var sub = DealerSubscription.CreatePending(Guid.NewGuid(), "cus_1", "price_1");

        sub.MarkPastDue();

        sub.Status.Should().Be(SubscriptionStatus.Incomplete);
    }

    [Fact]
    public void Suspend_And_Cancel_FromIncomplete_ShouldBeAllowed()
    {
        var suspended = DealerSubscription.CreatePending(Guid.NewGuid(), "cus_1", "price_1");
        suspended.Suspend();
        suspended.Status.Should().Be(SubscriptionStatus.Suspended);

        var cancelled = DealerSubscription.CreatePending(Guid.NewGuid(), "cus_2", "price_1");
        cancelled.Cancel();
        cancelled.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void AttachStripeCustomer_ShouldSetCustomer_OnlyWhenMissing()
    {
        var sub = DealerSubscription.CreatePending(Guid.NewGuid(), null, string.Empty);

        sub.AttachStripeCustomer("cus_9");
        sub.AttachStripeCustomer("cus_other");

        sub.StripeCustomerId.Should().Be("cus_9", "an existing customer link is never overwritten");
    }

    [Fact]
    public void Mapper_ShouldMapIncompleteToIncompleteStanding()
    {
        BillingStandingMapper.From(SubscriptionStatus.Incomplete).Should().Be(BillingStanding.Incomplete);
        BillingStandingMapper.From(null).Should().Be(BillingStanding.NoSubscription);
    }
}
