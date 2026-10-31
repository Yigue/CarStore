using System;

namespace Domain.Leads;

/// <summary>
/// sfc C1.4 / B13: idempotency guard for the Meta Leads Ads webhook. Meta retries the webhook on any non-2xx
/// response and resends the same <c>leadgen_id</c> on retry; this table makes a replay a no-op. Mirrors
/// <c>Domain.Billing.ProcessedStripeEvent</c>'s dedicated dedup-table pattern for Stripe webhook events.
/// </summary>
public sealed class ProcessedMetaLeadEvent
{
    public string LeadgenId { get; set; } = string.Empty;
    public DateTime ProcessedOnUtc { get; set; }
    public Guid? DealerId { get; set; }

    /// <summary>Set once the Lead is created/appended; null for a lead that failed to produce usable field data.</summary>
    public Guid? LeadId { get; set; }
}
