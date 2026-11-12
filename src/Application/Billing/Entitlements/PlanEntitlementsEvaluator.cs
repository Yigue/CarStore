using SharedKernel;

namespace Application.Billing.Entitlements;

/// <summary>
/// sfc A3 — pure plan-entitlement evaluator (RN-011, RN-012, A21, A24). No I/O: the caller
/// loads the limits (catalog) and the usage (counts) and passes them in. A null
/// <see cref="PlanLimits"/> (unknown plan / no PlanCode) or an undeclared limit fails open.
/// </summary>
public static class PlanEntitlementsEvaluator
{
    private static readonly PlanResource[] AllResources = Enum.GetValues<PlanResource>();

    public static Result CanCreate(PlanLimits? limits, PlanUsage usage, PlanResource resource)
    {
        ArgumentNullException.ThrowIfNull(usage);

        int? limit = limits?.For(resource);
        if (limit is null)
            return Result.Success();

        int used = usage.For(resource);
        return used < limit.Value
            ? Result.Success()
            : Result.Failure(LimitExceeded(resource, used, limit.Value));
    }

    public static IReadOnlyList<PlanUsageLine> Report(PlanLimits? limits, PlanUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        return AllResources
            .Select(r =>
            {
                int? limit = limits?.For(r);
                int used = usage.For(r);
                return new PlanUsageLine(r, used, limit, limit is not null && used > limit.Value);
            })
            .ToList();
    }

    public static Result<DowngradeAssessment> EvaluateDowngrade(
        PlanLimits? target,
        PlanUsage usage,
        DowngradePolicy policy = DowngradePolicy.AllowAndFlagOverLimit)
    {
        ArgumentNullException.ThrowIfNull(usage);

        List<PlanResource> exceeded = AllResources
            .Where(r => target?.For(r) is { } limit && usage.For(r) > limit)
            .ToList();

        if (exceeded.Count > 0 && policy == DowngradePolicy.RejectIfExceeds)
            return Result.Failure<DowngradeAssessment>(Error.Conflict(
                "PLAN_DOWNGRADE_EXCEEDS_LIMITS",
                $"The target plan limits are below current usage for: {string.Join(", ", exceeded)}."));

        return new DowngradeAssessment(exceeded.Count > 0, exceeded);
    }

    private static Error LimitExceeded(PlanResource resource, int used, int limit) => resource switch
    {
        PlanResource.Webhooks => Error.Forbidden(
            "WEBHOOK_QUOTA_EXCEEDED", "Alcanzaste el límite de webhooks de tu plan"),
        PlanResource.Vehicles => Error.Forbidden(
            "PLAN_LIMIT_EXCEEDED", $"Alcanzaste el límite de vehículos de tu plan ({used}/{limit})"),
        _ => Error.Forbidden(
            "PLAN_LIMIT_EXCEEDED", $"Alcanzaste el límite de {resource} de tu plan ({used}/{limit})"),
    };
}
