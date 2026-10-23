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
}
