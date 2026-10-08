namespace Domain.Billing;

/// <summary>
/// spr ADR B7 — pure resolver combining the manual switch (<c>IsActive</c>, 403) and the
/// subscription standing (402). Precedence: manual &gt; billing. It does not replace the
/// existing middlewares; it is the single, testable source of the rule (anti-drift test).
/// </summary>
public static class DealerOperationalStatusResolver
{
    /// <summary>PastDue tolerance before access is blocked (configurable per call; default 7 days).</summary>
    public static readonly TimeSpan DefaultPastDueGrace = TimeSpan.FromDays(7);

    public static DealerOperationalStatus Resolve(bool isActive, BillingStanding billing)
    {
        if (!isActive)
            return DealerOperationalStatus.SuspendedManual;

        return billing switch
        {
            BillingStanding.Active or BillingStanding.Trialing => DealerOperationalStatus.Active,
            BillingStanding.PastDue => DealerOperationalStatus.PastDue,
            BillingStanding.Suspended => DealerOperationalStatus.Suspended,
            BillingStanding.Cancelled => DealerOperationalStatus.Cancelled,
            _ => DealerOperationalStatus.BillingPending, // NoSubscription / Incomplete
        };
    }

    /// <param name="pastDueSinceUtc">When the subscription became PastDue; unknown → fail closed.</param>
    /// <param name="pastDueGrace">Grace window; defaults to <see cref="DefaultPastDueGrace"/>.</param>
    public static DealerAccessDecision DecideAccess(
        bool isActive,
        BillingStanding billing,
        DateTime? pastDueSinceUtc,
        DateTime nowUtc,
        TimeSpan? pastDueGrace = null)
    {
        if (!isActive)
            return DealerAccessDecision.ForbiddenManualSuspension;

        return billing switch
        {
            BillingStanding.Active or BillingStanding.Trialing => DealerAccessDecision.Allowed,
            BillingStanding.PastDue => IsWithinGrace(pastDueSinceUtc, nowUtc, pastDueGrace ?? DefaultPastDueGrace)
                ? DealerAccessDecision.Allowed
                : DealerAccessDecision.PaymentRequired,
            _ => DealerAccessDecision.PaymentRequired,
        };
    }

    private static bool IsWithinGrace(DateTime? since, DateTime nowUtc, TimeSpan grace) =>
        since is not null && nowUtc - since.Value <= grace;
}
