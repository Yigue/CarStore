using System.Linq;
using Application.Billing.Entitlements;
using FluentAssertions;
using SharedKernel;
using Xunit;

namespace ApplicationTests.Billing.Entitlements;

/// <summary>sfc A3 / spec plan-entitlements: limits per plan, fail-open (RN-011), downgrade (RN-012).</summary>
public class PlanEntitlementsEvaluatorTests
{
    private static readonly PlanLimits Standard = new(MaxVehicles: 50, MaxUsers: 5, MaxApiKeys: 3, MaxWebhooks: 5);

    private static PlanUsage Usage(int vehicles = 0, int users = 0, int apiKeys = 0, int webhooks = 0) =>
        new(vehicles, users, apiKeys, webhooks);

    // ── RN-011: can-create ───────────────────────────────────────────────────────────────────

    [Fact]
    public void RN011_CanCreate_BelowLimit_IsAllowed()
    {
        PlanEntitlementsEvaluator.CanCreate(Standard, Usage(vehicles: 49), PlanResource.Vehicles)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RN011_CanCreate_AtLimit_IsRejectedWithPlanLimitExceeded()
    {
        Result r = PlanEntitlementsEvaluator.CanCreate(Standard, Usage(vehicles: 50), PlanResource.Vehicles);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("PLAN_LIMIT_EXCEEDED");
        r.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void RN011_CanCreate_AboveLimit_IsRejected()
    {
        PlanEntitlementsEvaluator.CanCreate(Standard, Usage(users: 9), PlanResource.Users)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RN011_CanCreate_UnknownPlan_FailsOpen()
    {
        PlanEntitlementsEvaluator.CanCreate(null, Usage(vehicles: 10_000), PlanResource.Vehicles)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RN011_CanCreate_ResourceWithoutDeclaredLimit_FailsOpen()
    {
        var partial = new PlanLimits(MaxVehicles: 50, MaxUsers: null, MaxApiKeys: null, MaxWebhooks: null);

        PlanEntitlementsEvaluator.CanCreate(partial, Usage(users: 999), PlanResource.Users)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void A24_CanCreate_WebhookQuotaExceeded_UsesWebhookCode()
    {
        Result r = PlanEntitlementsEvaluator.CanCreate(Standard, Usage(webhooks: 5), PlanResource.Webhooks);

        r.Error.Code.Should().Be("WEBHOOK_QUOTA_EXCEEDED");
    }

    [Fact]
    public void A24_CanCreate_WebhookWithinQuota_IsAllowed()
    {
        PlanEntitlementsEvaluator.CanCreate(Standard, Usage(webhooks: 2), PlanResource.Webhooks)
            .IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(PlanResource.Vehicles)]
    [InlineData(PlanResource.Users)]
    [InlineData(PlanResource.ApiKeys)]
    [InlineData(PlanResource.Webhooks)]
    public void RN011_CanCreate_EveryResourceIsCheckedAgainstItsOwnLimit(PlanResource resource)
    {
        var limits = new PlanLimits(1, 1, 1, 1);
        PlanUsage full = Usage(1, 1, 1, 1);

        PlanEntitlementsEvaluator.CanCreate(limits, full, resource).IsFailure.Should().BeTrue();
        PlanEntitlementsEvaluator.CanCreate(limits, Usage(), resource).IsSuccess.Should().BeTrue();
    }

    // ── usage report (A21) ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A21_Report_ShowsUsedVsLimitAndOverLimit()
    {
        var report = PlanEntitlementsEvaluator.Report(Standard, Usage(vehicles: 30, users: 6));

        report.Single(l => l.Resource == PlanResource.Vehicles).Should().Be(new PlanUsageLine(PlanResource.Vehicles, 30, 50, false));
        report.Single(l => l.Resource == PlanResource.Users).OverLimit.Should().BeTrue();
    }

    [Fact]
    public void A21_Report_UnknownPlan_HasNullLimitsAndNeverOverLimit()
    {
        var report = PlanEntitlementsEvaluator.Report(null, Usage(vehicles: 1000));

        report.Should().OnlyContain(l => l.Limit == null && !l.OverLimit);
    }

    // ── RN-012: downgrade ────────────────────────────────────────────────────────────────────

    [Fact]
    public void RN012_Downgrade_ExceedingTarget_IsAllowedAndFlagsOverLimit_ByDefault()
    {
        var target = new PlanLimits(50, 5, 3, 5);

        Result<DowngradeAssessment> r = PlanEntitlementsEvaluator.EvaluateDowngrade(target, Usage(vehicles: 80));

        r.IsSuccess.Should().BeTrue();
        r.Value.IsOverLimit.Should().BeTrue();
        r.Value.ExceededResources.Should().Equal(PlanResource.Vehicles);
    }

    [Fact]
    public void RN012_Downgrade_WithinTarget_IsNotFlagged()
    {
        Result<DowngradeAssessment> r = PlanEntitlementsEvaluator.EvaluateDowngrade(Standard, Usage(vehicles: 50, users: 5));

        r.Value.IsOverLimit.Should().BeFalse();
        r.Value.ExceededResources.Should().BeEmpty();
    }

    [Fact]
    public void RN012_Downgrade_StrictPolicy_RejectsWhenUsageExceedsTarget()
    {
        Result<DowngradeAssessment> r = PlanEntitlementsEvaluator.EvaluateDowngrade(
            Standard, Usage(vehicles: 80, webhooks: 9), DowngradePolicy.RejectIfExceeds);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("PLAN_DOWNGRADE_EXCEEDS_LIMITS");
    }

    [Fact]
    public void RN012_Downgrade_StrictPolicy_WithinTarget_Passes()
    {
        PlanEntitlementsEvaluator.EvaluateDowngrade(Standard, Usage(vehicles: 10), DowngradePolicy.RejectIfExceeds)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RN011_Downgrade_ToUnknownPlan_FailsOpen()
    {
        PlanEntitlementsEvaluator.EvaluateDowngrade(null, Usage(vehicles: 999), DowngradePolicy.RejectIfExceeds)
            .Value.IsOverLimit.Should().BeFalse();
    }

    [Fact]
    public void RN012_OverLimitDealer_CannotCreateUntilRegularized_ButCanAfterDeleting()
    {
        PlanEntitlementsEvaluator.CanCreate(Standard, Usage(vehicles: 80), PlanResource.Vehicles).IsFailure.Should().BeTrue();
        PlanEntitlementsEvaluator.CanCreate(Standard, Usage(vehicles: 49), PlanResource.Vehicles).IsSuccess.Should().BeTrue();
    }
}
