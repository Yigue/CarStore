namespace Application.Billing.Entitlements;

/// <summary>Limits declared by a plan. A null limit means "not declared" → unlimited (fail-open, RN-011).</summary>
public sealed record PlanLimits(int? MaxVehicles, int? MaxUsers, int? MaxApiKeys, int? MaxWebhooks)
{
    public int? For(PlanResource resource) => resource switch
    {
        PlanResource.Vehicles => MaxVehicles,
        PlanResource.Users => MaxUsers,
        PlanResource.ApiKeys => MaxApiKeys,
        PlanResource.Webhooks => MaxWebhooks,
        _ => null,
    };
}

/// <summary>Current consumption of a dealer.</summary>
public sealed record PlanUsage(int Vehicles, int Users, int ApiKeys, int Webhooks)
{
    public int For(PlanResource resource) => resource switch
    {
        PlanResource.Vehicles => Vehicles,
        PlanResource.Users => Users,
        PlanResource.ApiKeys => ApiKeys,
        PlanResource.Webhooks => Webhooks,
        _ => 0,
    };
}

/// <summary>One row of <c>GET /dealers/me/usage</c> (A21).</summary>
public sealed record PlanUsageLine(PlanResource Resource, int Used, int? Limit, bool OverLimit);

public enum DowngradePolicy
{
    /// <summary>RN-012 (spec billing-growth): never retroactive; allow and flag over-limit.</summary>
    AllowAndFlagOverLimit,
    /// <summary>Stricter alternative: refuse a downgrade whose target limits are below current usage.</summary>
    RejectIfExceeds
}

public sealed record DowngradeAssessment(bool IsOverLimit, IReadOnlyList<PlanResource> ExceededResources);
