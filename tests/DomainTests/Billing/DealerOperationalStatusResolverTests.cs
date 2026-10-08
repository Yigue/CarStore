using Domain.Billing;

namespace DomainTests.Billing;

/// <summary>spr ADR B7 / dealer-operational-status: manual (403) &gt; billing (402), pure projection.</summary>
public class DealerOperationalStatusResolverTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    // ── projection: operationalStatus ────────────────────────────────────────────────────────

    public static TheoryData<bool, BillingStanding, DealerOperationalStatus> StatusTable => new()
    {
        { true,  BillingStanding.Active,         DealerOperationalStatus.Active },
        { true,  BillingStanding.Trialing,       DealerOperationalStatus.Active },
        { true,  BillingStanding.PastDue,        DealerOperationalStatus.PastDue },
        { true,  BillingStanding.Suspended,      DealerOperationalStatus.Suspended },
        { true,  BillingStanding.Cancelled,      DealerOperationalStatus.Cancelled },
        { true,  BillingStanding.Incomplete,     DealerOperationalStatus.BillingPending },
        { true,  BillingStanding.NoSubscription, DealerOperationalStatus.BillingPending },
        // manual suspension wins over every billing state
        { false, BillingStanding.Active,         DealerOperationalStatus.SuspendedManual },
        { false, BillingStanding.Trialing,       DealerOperationalStatus.SuspendedManual },
        { false, BillingStanding.PastDue,        DealerOperationalStatus.SuspendedManual },
        { false, BillingStanding.Suspended,      DealerOperationalStatus.SuspendedManual },
        { false, BillingStanding.Cancelled,      DealerOperationalStatus.SuspendedManual },
        { false, BillingStanding.Incomplete,     DealerOperationalStatus.SuspendedManual },
        { false, BillingStanding.NoSubscription, DealerOperationalStatus.SuspendedManual },
    };

    [Theory]
    [MemberData(nameof(StatusTable))]
    public void Resolve_FollowsPrecedenceTable(bool isActive, BillingStanding billing, DealerOperationalStatus expected)
    {
        DealerOperationalStatusResolver.Resolve(isActive, billing).Should().Be(expected);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, BillingStanding.Active)]
    [InlineData(SubscriptionStatus.Trialing, BillingStanding.Trialing)]
    [InlineData(SubscriptionStatus.PastDue, BillingStanding.PastDue)]
    [InlineData(SubscriptionStatus.Suspended, BillingStanding.Suspended)]
    [InlineData(SubscriptionStatus.Cancelled, BillingStanding.Cancelled)]
    public void FromSubscription_MapsEveryEnumValue(SubscriptionStatus status, BillingStanding expected)
    {
        BillingStandingMapper.From(status).Should().Be(expected);
    }

    [Fact]
    public void FromSubscription_NoRow_IsNoSubscription()
    {
        BillingStandingMapper.From(null).Should().Be(BillingStanding.NoSubscription);
    }

    [Fact]
    public void FromSubscription_CoversEverySubscriptionStatus()
    {
        foreach (SubscriptionStatus s in Enum.GetValues<SubscriptionStatus>())
            BillingStandingMapper.From(s).Should().NotBe(BillingStanding.NoSubscription);
    }

    // ── enforcement decision: 403 / 402 / allowed ────────────────────────────────────────────

    [Theory]
    [InlineData(BillingStanding.Active)]
    [InlineData(BillingStanding.Trialing)]
    [InlineData(BillingStanding.Incomplete)]
    [InlineData(BillingStanding.NoSubscription)]
    [InlineData(BillingStanding.PastDue)]
    public void DecideAccess_ManualSuspension_Is403_WhateverTheBilling(BillingStanding billing)
    {
        DealerOperationalStatusResolver.DecideAccess(false, billing, null, Now)
            .Should().Be(DealerAccessDecision.ForbiddenManualSuspension);
    }

    [Theory]
    [InlineData(BillingStanding.Active)]
    [InlineData(BillingStanding.Trialing)]
    public void DecideAccess_ActiveOrTrialing_IsAllowed(BillingStanding billing)
    {
        DealerOperationalStatusResolver.DecideAccess(true, billing, null, Now)
            .Should().Be(DealerAccessDecision.Allowed);
    }

    [Theory]
    [InlineData(BillingStanding.Incomplete)]
    [InlineData(BillingStanding.NoSubscription)]
    [InlineData(BillingStanding.Suspended)]
    [InlineData(BillingStanding.Cancelled)]
    public void DecideAccess_NoActiveBilling_Is402(BillingStanding billing)
    {
        DealerOperationalStatusResolver.DecideAccess(true, billing, null, Now)
            .Should().Be(DealerAccessDecision.PaymentRequired);
    }

    [Fact]
    public void DecideAccess_PastDueWithinGrace_IsAllowed()
    {
        DealerOperationalStatusResolver.DecideAccess(true, BillingStanding.PastDue, Now.AddDays(-6), Now)
            .Should().Be(DealerAccessDecision.Allowed);
    }

    [Fact]
    public void DecideAccess_PastDueExactlyAtGraceEnd_IsStillAllowed()
    {
        DealerOperationalStatusResolver.DecideAccess(true, BillingStanding.PastDue, Now.AddDays(-7), Now)
            .Should().Be(DealerAccessDecision.Allowed);
    }

    [Fact]
    public void DecideAccess_PastDueBeyondGrace_Is402()
    {
        DealerOperationalStatusResolver.DecideAccess(true, BillingStanding.PastDue, Now.AddDays(-7).AddSeconds(-1), Now)
            .Should().Be(DealerAccessDecision.PaymentRequired);
    }

    [Fact]
    public void DecideAccess_PastDueGraceIsConfigurable()
    {
        DealerOperationalStatusResolver.DecideAccess(true, BillingStanding.PastDue, Now.AddDays(-4), Now, TimeSpan.FromDays(3))
            .Should().Be(DealerAccessDecision.PaymentRequired);
        DealerOperationalStatusResolver.DecideAccess(true, BillingStanding.PastDue, Now.AddDays(-4), Now, TimeSpan.FromDays(10))
            .Should().Be(DealerAccessDecision.Allowed);
    }

    [Fact]
    public void DecideAccess_PastDueWithUnknownSince_FailsClosed()
    {
        DealerOperationalStatusResolver.DecideAccess(true, BillingStanding.PastDue, null, Now)
            .Should().Be(DealerAccessDecision.PaymentRequired);
    }

    [Fact]
    public void DefaultGraceIsSevenDays()
    {
        DealerOperationalStatusResolver.DefaultPastDueGrace.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void BillingPending_NeverReadsAsFreeAccess()
    {
        // B8: BillingPending is a projection label, the enforcement answer is 402.
        DealerOperationalStatusResolver.Resolve(true, BillingStanding.Incomplete).Should().Be(DealerOperationalStatus.BillingPending);
        DealerOperationalStatusResolver.DecideAccess(true, BillingStanding.Incomplete, null, Now)
            .Should().Be(DealerAccessDecision.PaymentRequired);
    }
}
