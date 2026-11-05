using Application.Tenancy;
using Domain.Billing;
using Domain.DealerSettings;

namespace ApplicationTests.Billing;

/// <summary>
/// spr PR 3.1 (ADR B8, D2), task 3.1.4: wiring-level coverage of the PastDue grace period through
/// <see cref="TenantAccessPolicy.Evaluate"/>. The pure grace math itself (<c>IsWithinGrace</c>) is
/// already covered end to end by <c>DomainTests.Billing.DealerOperationalStatusResolverTests</c>;
/// this suite instead proves the real consumer (the policy the HTTP guard middleware calls) actually
/// reuses that resolver instead of the old immediate-402 binary check.
/// </summary>
public class PastDueGraceTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PastDue_WithinConfiguredGrace_IsAllowed()
    {
        var decision = TenantAccessPolicy.Evaluate(
            SubscriptionStatus.PastDue,
            TenantLifecycleStatus.Active,
            "GET",
            "/api/v1/clients",
            pastDueSinceUtc: Now.AddDays(-3),
            pastDueGrace: TimeSpan.FromDays(7),
            nowUtc: Now);

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public void PastDue_BeyondConfiguredGrace_Is402Suspended()
    {
        var decision = TenantAccessPolicy.Evaluate(
            SubscriptionStatus.PastDue,
            TenantLifecycleStatus.Active,
            "GET",
            "/api/v1/clients",
            pastDueSinceUtc: Now.AddDays(-8),
            pastDueGrace: TimeSpan.FromDays(7),
            nowUtc: Now);

        decision.Allowed.Should().BeFalse();
        decision.Error.Should().Be(TenantAccessPolicy.SuspendedError);
    }

    [Fact]
    public void PastDue_ExactlyAtGraceBoundary_IsStillAllowed()
    {
        var decision = TenantAccessPolicy.Evaluate(
            SubscriptionStatus.PastDue,
            TenantLifecycleStatus.Active,
            "GET",
            "/api/v1/clients",
            pastDueSinceUtc: Now.AddDays(-7),
            pastDueGrace: TimeSpan.FromDays(7),
            nowUtc: Now);

        decision.Allowed.Should().BeTrue();
    }

    [Fact]
    public void PastDue_WithUnknownSince_FailsClosed_EvenWithinWhatWouldOtherwiseBeGrace()
    {
        // No pastDueSinceUtc provided (e.g. the cache anchor was never recorded, or was evicted) —
        // must deny immediately, the same fail-closed behavior as before this grace period existed.
        var decision = TenantAccessPolicy.Evaluate(
            SubscriptionStatus.PastDue,
            TenantLifecycleStatus.Active,
            "GET",
            "/api/v1/clients",
            pastDueSinceUtc: null,
            pastDueGrace: TimeSpan.FromDays(7),
            nowUtc: Now);

        decision.Allowed.Should().BeFalse();
        decision.Error.Should().Be(TenantAccessPolicy.SuspendedError);
    }

    [Fact]
    public void PastDue_GraceIsConfigurable_ShorterWindowDeniesSoonerThanDefault()
    {
        var since = Now.AddDays(-4);

        TenantAccessPolicy.Evaluate(
            SubscriptionStatus.PastDue, TenantLifecycleStatus.Active, "GET", "/api/v1/clients",
            pastDueSinceUtc: since, pastDueGrace: TimeSpan.FromDays(3), nowUtc: Now)
            .Allowed.Should().BeFalse("3-day grace already elapsed 1 day ago");

        TenantAccessPolicy.Evaluate(
            SubscriptionStatus.PastDue, TenantLifecycleStatus.Active, "GET", "/api/v1/clients",
            pastDueSinceUtc: since, pastDueGrace: TimeSpan.FromDays(10), nowUtc: Now)
            .Allowed.Should().BeTrue("10-day grace has not elapsed yet");
    }

    [Fact]
    public void PastDue_OmittedGrace_FallsBackToResolverDefault_SevenDays()
    {
        // No pastDueGrace passed: Evaluate must default to DealerOperationalStatusResolver.DefaultPastDueGrace.
        TenantAccessPolicy.Evaluate(
            SubscriptionStatus.PastDue, TenantLifecycleStatus.Active, "GET", "/api/v1/clients",
            pastDueSinceUtc: Now.AddDays(-6), nowUtc: Now)
            .Allowed.Should().BeTrue("6 days is within the 7-day default grace");

        TenantAccessPolicy.Evaluate(
            SubscriptionStatus.PastDue, TenantLifecycleStatus.Active, "GET", "/api/v1/clients",
            pastDueSinceUtc: Now.AddDays(-8), nowUtc: Now)
            .Allowed.Should().BeFalse("8 days exceeds the 7-day default grace");

        DealerOperationalStatusResolver.DefaultPastDueGrace.Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void ActiveOrTrialing_StillAllowed_RegardlessOfGraceParameters()
    {
        // Sanity check: the grace machinery must not affect the paying statuses it was never meant to touch.
        TenantAccessPolicy.Evaluate(
            SubscriptionStatus.Active, TenantLifecycleStatus.Active, "POST", "/api/v1/clients",
            pastDueSinceUtc: Now.AddDays(-100), pastDueGrace: TimeSpan.Zero, nowUtc: Now)
            .Allowed.Should().BeTrue();

        TenantAccessPolicy.Evaluate(
            SubscriptionStatus.Trialing, TenantLifecycleStatus.Active, "POST", "/api/v1/clients",
            pastDueSinceUtc: null, nowUtc: Now)
            .Allowed.Should().BeTrue();
    }
}
