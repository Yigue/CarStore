namespace Domain.Billing;

/// <summary>Read-only unified projection shown to the super-admin (spr ADR B7).</summary>
public enum DealerOperationalStatus
{
    Active,
    SuspendedManual,
    BillingPending,
    PastDue,
    Suspended,
    Cancelled
}

/// <summary>
/// Billing standing of a dealer as input to the resolver. <c>Incomplete</c> and
/// <c>NoSubscription</c> are first-class so the resolver does not depend on whether
/// <see cref="SubscriptionStatus"/> already has an <c>Incomplete</c> member (pending in qa-remediation).
/// </summary>
public enum BillingStanding
{
    NoSubscription,
    Incomplete,
    Active,
    Trialing,
    PastDue,
    Suspended,
    Cancelled
}

/// <summary>Enforcement answer for a tenant-scoped request.</summary>
public enum DealerAccessDecision
{
    Allowed,
    /// <summary>Manual suspension (<c>DealerSettings.IsActive == false</c>) → HTTP 403.</summary>
    ForbiddenManualSuspension,
    /// <summary>Billing does not entitle access → HTTP 402.</summary>
    PaymentRequired
}
