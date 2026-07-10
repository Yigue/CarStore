using Domain.Billing;
using FluentAssertions;
using System;
using Xunit;

namespace DomainTests.Billing;

public class DealerSubscriptionEdgeTests
{
    private static DealerSubscription CreateSubscription(Guid dealerId)
    {
        return DealerSubscription.Create(dealerId, "cus_123", "sub_123", "plan_123");
    }

    [Fact]
    public void RenewPeriod_ShouldBeIdempotentAndAllowMultipleCalls()
    {
        var sub = CreateSubscription(Guid.NewGuid());
        var start1 = DateTime.UtcNow;
        var end1 = start1.AddMonths(1);

        sub.RenewPeriod(start1, end1);
        sub.CurrentPeriodStart.Should().Be(start1);
        sub.CurrentPeriodEnd.Should().Be(end1);

        var start2 = DateTime.UtcNow.AddMinutes(5);
        var end2 = start2.AddMonths(1);

        sub.RenewPeriod(start2, end2);
        sub.CurrentPeriodStart.Should().Be(start2);
        sub.CurrentPeriodEnd.Should().Be(end2);
    }

    [Fact]
    public void Cancel_SetsCancelledAtToUtcNow()
    {
        var sub = CreateSubscription(Guid.NewGuid());
        sub.Cancel();

        sub.Status.Should().Be(SubscriptionStatus.Cancelled);
        sub.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Activate_AfterSuspend_ShouldBeAllowed()
    {
        var sub = CreateSubscription(Guid.NewGuid());
        sub.Activate("sub_123", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub.Suspend();

        sub.Status.Should().Be(SubscriptionStatus.Suspended);

        var start = DateTime.UtcNow;
        var end = start.AddMonths(1);
        sub.Activate("sub_123", start, end);

        sub.Status.Should().Be(SubscriptionStatus.Active);
        sub.CurrentPeriodStart.Should().Be(start);
        sub.CurrentPeriodEnd.Should().Be(end);
    }
}
