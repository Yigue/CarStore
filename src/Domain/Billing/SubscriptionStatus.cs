namespace Domain.Billing;

public enum SubscriptionStatus
{
    Active = 1,
    Trialing = 2,
    PastDue = 3,
    Suspended = 4,
    Cancelled = 5
}
