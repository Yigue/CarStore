using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Billing;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    [Required(AllowEmptyStrings = false)]
    public string SecretKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Legacy single-price setting (<c>Stripe__PriceId</c>). spr PR 1.1 (ADR B1): kept for back-compat —
    /// when <see cref="Plans"/> is empty, <see cref="ResolvePlans"/> synthesizes a single
    /// <see cref="DefaultPlanCode"/> entry from this value so existing CI/deployment config keeps working.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string PriceId { get; set; } = string.Empty;

    public int TrialDays { get; set; } = 14;

    /// <summary>spr PR 1.1 (ADR B1, D1): the plan code used when a webhook event carries no metadata.</summary>
    public string DefaultPlanCode { get; set; } = "standard";

    /// <summary>
    /// spr PR 1.1 (ADR B1): plan code → Stripe price id. Empty by default (no deployment configures this
    /// yet); <see cref="ResolvePlans"/> is the single place that decides what is actually sellable.
    /// </summary>
    public Dictionary<string, StripePlanOptions> Plans { get; set; } = new();

    /// <summary>
    /// "Plan único" (D1): the effective plan catalog. When <see cref="Plans"/> is configured directly it
    /// wins; otherwise a single <see cref="DefaultPlanCode"/> entry is synthesized from <see cref="PriceId"/>
    /// so <c>Stripe__PriceId</c> keeps working unchanged. Never mutates <see cref="Plans"/>.
    /// </summary>
    public IReadOnlyDictionary<string, StripePlanOptions> ResolvePlans()
    {
        if (Plans.Count > 0)
        {
            return Plans;
        }

        if (string.IsNullOrWhiteSpace(PriceId))
        {
            return new Dictionary<string, StripePlanOptions>();
        }

        return new Dictionary<string, StripePlanOptions>
        {
            [DefaultPlanCode] = new StripePlanOptions { PriceId = PriceId }
        };
    }
}

/// <summary>spr PR 1.1 (ADR B1): one sellable plan's Stripe price id. Grows as multi-plan support lands.</summary>
public sealed class StripePlanOptions
{
    [Required(AllowEmptyStrings = false)]
    public string PriceId { get; set; } = string.Empty;

    /// <summary>
    /// sfc A3.1 (A1, A21, RN-011): optional numeric limits for this plan — <c>Billing:Plans:{code}:Limits</c>.
    /// Absent (null) means the plan declares no limits at all (fail-open, unlimited); within a configured
    /// <see cref="StripePlanLimitsOptions"/>, each individual <c>Max*</c> left unset is also unlimited for
    /// that resource only — see <see cref="StripePlanLimitsOptions"/>.
    /// </summary>
    public StripePlanLimitsOptions? Limits { get; set; }

    /// <summary>
    /// sfc A3.2 (A2, A24, RN-020, RN-021): feature flags included in this plan — <c>Billing:Plans:{code}:Features</c>.
    /// Empty by default (no deployment configures this yet).
    /// </summary>
    public List<string> Features { get; set; } = new();
}

/// <summary>
/// sfc A3.1: numeric plan limits read from <c>Billing:Plans:{code}:Limits:*</c>. Every property is an
/// optional int — unset (null) means "not declared" → unlimited for that resource (RN-011 fail-open),
/// mirrored 1:1 onto <see cref="Application.Billing.Entitlements.PlanLimits"/>.
/// </summary>
public sealed class StripePlanLimitsOptions
{
    public int? MaxVehicles { get; set; }

    public int? MaxUsers { get; set; }

    public int? MaxApiKeys { get; set; }

    public int? MaxWebhooks { get; set; }
}
