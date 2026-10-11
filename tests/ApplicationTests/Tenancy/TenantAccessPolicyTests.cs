using Application.Tenancy;
using Domain.Billing;
using Domain.DealerSettings;
using FluentAssertions;
using Xunit;

namespace Application.UnitTests.Tenancy;

/// <summary>
/// RN-013 (A6, H9): the access matrix is subscription status x tenant lifecycle x HTTP method x path.
/// CancelledGrace is read-only (+ export); Locked blocks everything except the escape-hatch routes.
/// </summary>
public class TenantAccessPolicyTests
{
    private const string Cars = "/api/v1/cars";
    private const string Exports = "/api/v1/dealer/exports";
    private const string ExportDownload = "/api/v1/dealer/exports/3f2b8f0e-1111-2222-3333-444455556666/download";

    private static TenantAccessDecision Evaluate(
        SubscriptionStatus? status, TenantLifecycleStatus lifecycle, string method, string path) =>
        TenantAccessPolicy.Evaluate(status, lifecycle, method, path);

    // ---- Active lifecycle: keeps the pre-existing binary behaviour (fail closed) ----

    [Theory]
    [InlineData(SubscriptionStatus.Active)]
    [InlineData(SubscriptionStatus.Trialing)]
    public void ActiveLifecycle_PayingSubscription_AllowsEverything(SubscriptionStatus status)
    {
        Evaluate(status, TenantLifecycleStatus.Active, "POST", Cars).Allowed.Should().BeTrue();
        Evaluate(status, TenantLifecycleStatus.Active, "DELETE", Cars).Allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SubscriptionStatus.Suspended)]
    [InlineData(SubscriptionStatus.PastDue)]
    [InlineData(SubscriptionStatus.Incomplete)]
    [InlineData(SubscriptionStatus.Cancelled)]
    public void ActiveLifecycle_NonPayingOrMissing_Blocks402Suspended(SubscriptionStatus? status)
    {
        var decision = Evaluate(status, TenantLifecycleStatus.Active, "GET", Cars);

        decision.Allowed.Should().BeFalse();
        decision.Error.Should().Be("subscription.suspended");
    }

    // ---- CancelledGrace: read-only ----

    [Theory]
    [InlineData("GET", Cars)]
    [InlineData("HEAD", Cars)]
    [InlineData("get", Cars)]
    [InlineData("POST", Exports)]
    [InlineData("GET", ExportDownload)]
    public void CancelledGrace_AllowsReadsAndExportRequests(string method, string path)
    {
        Evaluate(SubscriptionStatus.Cancelled, TenantLifecycleStatus.CancelledGrace, method, path)
            .Allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("POST", Cars)]
    [InlineData("PUT", Cars)]
    [InlineData("PATCH", Cars)]
    [InlineData("DELETE", Cars)]
    [InlineData("DELETE", Exports)]
    [InlineData("PUT", Exports)]
    [InlineData("POST", "/api/v1/dealer/exports/abc")]
    [InlineData("POST", "/api/v1/dealer/exports-evil")]
    public void CancelledGrace_BlocksOtherWritesWithReadOnly(string method, string path)
    {
        var decision = Evaluate(SubscriptionStatus.Cancelled, TenantLifecycleStatus.CancelledGrace, method, path);

        decision.Allowed.Should().BeFalse();
        decision.Error.Should().Be("subscription.read_only");
    }

    [Fact]
    public void CancelledGrace_DoesNotDependOnTheCachedSubscriptionStatus()
    {
        // The status cache can be missing/stale right after the webhook; the lifecycle is the source of truth.
        Evaluate(null, TenantLifecycleStatus.CancelledGrace, "GET", Cars).Allowed.Should().BeTrue();
        Evaluate(null, TenantLifecycleStatus.CancelledGrace, "POST", Cars).Error
            .Should().Be("subscription.read_only");
    }

    // ---- Locked / Purged: everything blocked except escape hatches ----

    [Theory]
    [InlineData("GET", Cars)]
    [InlineData("HEAD", Cars)]
    [InlineData("POST", Cars)]
    [InlineData("POST", Exports)]
    [InlineData("DELETE", Cars)]
    public void Locked_BlocksEverythingElse(string method, string path)
    {
        var decision = Evaluate(SubscriptionStatus.Cancelled, TenantLifecycleStatus.Locked, method, path);

        decision.Allowed.Should().BeFalse();
        decision.Error.Should().Be("subscription.locked");
    }

    [Theory]
    [InlineData("POST", "/api/v1/users/login")]
    [InlineData("GET", "/api/v1/users/me")]
    [InlineData("POST", "/api/v1/subscriptions/portal")]
    [InlineData("GET", ExportDownload)]
    public void Locked_AllowsLoginProfilePortalAndExportDownload(string method, string path)
    {
        Evaluate(SubscriptionStatus.Cancelled, TenantLifecycleStatus.Locked, method, path)
            .Allowed.Should().BeTrue();
    }

    [Fact]
    public void Locked_DoesNotAllowDownloadWithWriteMethod()
    {
        Evaluate(SubscriptionStatus.Cancelled, TenantLifecycleStatus.Locked, "DELETE", ExportDownload)
            .Allowed.Should().BeFalse();
    }

    [Fact]
    public void Purged_BehavesLikeLocked()
    {
        Evaluate(SubscriptionStatus.Cancelled, TenantLifecycleStatus.Purged, "GET", Cars).Error
            .Should().Be("subscription.locked");
        Evaluate(SubscriptionStatus.Cancelled, TenantLifecycleStatus.Purged, "GET", "/api/v1/users/me")
            .Allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, TenantLifecycleStatus.Locked, "POST", Cars)]
    [InlineData(SubscriptionStatus.Trialing, TenantLifecycleStatus.Purged, "POST", Cars)]
    [InlineData(SubscriptionStatus.Active, TenantLifecycleStatus.Locked, "GET", Cars)]
    public void PayingStatus_DoesNotOverrideLockedOrPurged(
        SubscriptionStatus status, TenantLifecycleStatus lifecycle, string method, string path)
    {
        var decision = Evaluate(status, lifecycle, method, path);

        decision.Allowed.Should().BeFalse();
        decision.Error.Should().Be("subscription.locked");
    }

    [Fact]
    public void PayingStatus_DoesNotOverrideCancelledGraceWrites()
    {
        var decision = Evaluate(SubscriptionStatus.Active, TenantLifecycleStatus.CancelledGrace, "POST", Cars);

        decision.Allowed.Should().BeFalse();
        decision.Error.Should().Be("subscription.read_only");
    }
}
