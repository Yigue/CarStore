namespace Domain.Billing;

public static class BillingStandingMapper
{
    /// <summary>Null means the dealer has no subscription row (legacy / not yet checked out).</summary>
    public static BillingStanding From(SubscriptionStatus? status) => status switch
    {
        null => BillingStanding.NoSubscription,
        SubscriptionStatus.Incomplete => BillingStanding.Incomplete,
        SubscriptionStatus.Active => BillingStanding.Active,
        SubscriptionStatus.Trialing => BillingStanding.Trialing,
        SubscriptionStatus.PastDue => BillingStanding.PastDue,
        SubscriptionStatus.Suspended => BillingStanding.Suspended,
        SubscriptionStatus.Cancelled => BillingStanding.Cancelled,
        _ => BillingStanding.Suspended, // unknown future value: fail closed
    };
}
