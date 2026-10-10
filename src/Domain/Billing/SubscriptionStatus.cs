namespace Domain.Billing;

public enum SubscriptionStatus
{
    Active = 1,
    Trialing = 2,
    PastDue = 3,
    Suspended = 4,
    Cancelled = 5,

    /// <summary>
    /// spr ADR B8: the dealer was provisioned but has no confirmed payment yet. Blocked (402) like
    /// any non-paying state, so the product is never used for free while checkout is pending.
    /// Persisted as a string column, so adding it needs no schema change.
    /// </summary>
    Incomplete = 6
}
