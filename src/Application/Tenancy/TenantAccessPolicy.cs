using System;
using System.Text.RegularExpressions;
using Domain.Billing;
using Domain.DealerSettings;

namespace Application.Tenancy;

/// <summary>Outcome of <see cref="TenantAccessPolicy.Evaluate"/>. <see cref="Error"/> is the 402 error code when blocked.</summary>
public readonly record struct TenantAccessDecision(bool Allowed, string? Error)
{
    public static TenantAccessDecision Allow { get; } = new(true, null);
    public static TenantAccessDecision Deny(string error) => new(false, error);
}

/// <summary>
/// RN-013 (A6, H9): pure access matrix = subscription status x tenant lifecycle x HTTP method x path.
/// <list type="bullet">
/// <item>Active lifecycle: only Active/Trialing pass (fail closed, same as the former binary guard).</item>
/// <item>CancelledGrace: read-only. GET/HEAD and POST of an export pass; other writes get <c>subscription.read_only</c>.</item>
/// <item>Locked/Purged: everything is blocked (<c>subscription.locked</c>) except login, <c>users/me</c>,
/// the billing portal and the download of an already generated export.</item>
/// </list>
/// </summary>
public static class TenantAccessPolicy
{
    public const string SuspendedError = "subscription.suspended";
    public const string ReadOnlyError = "subscription.read_only";
    public const string LockedError = "subscription.locked";

    private static readonly Regex ExportRequestPath = new(
        @"^/api/v1/dealer/exports/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExportDownloadPath = new(
        @"^/api/v1/dealer/exports/[^/]+/download/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LockedEscapeHatchPath = new(
        @"^/api/v1/(users/(login|me)|subscriptions/portal)/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <param name="status">Cached subscription status; null means no row (legacy dealer).</param>
    /// <param name="lifecycle">Tenant lifecycle; takes precedence over billing per the matrix above.</param>
    /// <param name="method">HTTP method of the current request.</param>
    /// <param name="path">HTTP path of the current request.</param>
    /// <param name="pastDueSinceUtc">
    /// spr PR 3.1 (ADR B8, D2): when <paramref name="status"/> is <see cref="SubscriptionStatus.PastDue"/>,
    /// the UTC instant that standing began (from <see cref="Application.Abstractions.Billing.ISubscriptionStatusCache.GetPastDueSinceAsync"/>).
    /// Null (unknown) fails closed — same behavior as before this parameter existed.
    /// </param>
    /// <param name="pastDueGrace">
    /// Configurable PastDue tolerance (<c>Billing:PastDueGraceDays</c>, default 7). Null reuses
    /// <see cref="DealerOperationalStatusResolver.DefaultPastDueGrace"/>.
    /// </param>
    /// <param name="nowUtc">Injectable "now" for tests; defaults to <see cref="DateTime.UtcNow"/>.</param>
    public static TenantAccessDecision Evaluate(
        SubscriptionStatus? status,
        TenantLifecycleStatus lifecycle,
        string method,
        string path,
        DateTime? pastDueSinceUtc = null,
        TimeSpan? pastDueGrace = null,
        DateTime? nowUtc = null)
    {
        bool isRead = IsRead(method);
        bool isGet = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
        bool isPost = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

        switch (lifecycle)
        {
            case TenantLifecycleStatus.CancelledGrace:
                if (isRead || (isPost && ExportRequestPath.IsMatch(path)))
                    return TenantAccessDecision.Allow;
                return TenantAccessDecision.Deny(ReadOnlyError);

            case TenantLifecycleStatus.Locked:
            case TenantLifecycleStatus.Purged:
                if (LockedEscapeHatchPath.IsMatch(path) && (isGet || isPost))
                    return TenantAccessDecision.Allow;
                if (isGet && ExportDownloadPath.IsMatch(path))
                    return TenantAccessDecision.Allow;
                return TenantAccessDecision.Deny(LockedError);

            default:
                // spr PR 3.1 (ADR B8, D2): reuse the already-tested pure resolver instead of
                // reimplementing the PastDue grace period here. isActive is always true by this point —
                // a manually-suspended dealer (SuspendedManual) never reaches this branch; the manual
                // switch is enforced upstream (DealerSuspensionMiddleware), before the billing gate runs.
                var billing = BillingStandingMapper.From(status);
                var decision = DealerOperationalStatusResolver.DecideAccess(
                    isActive: true,
                    billing: billing,
                    pastDueSinceUtc: pastDueSinceUtc,
                    nowUtc: nowUtc ?? DateTime.UtcNow,
                    pastDueGrace: pastDueGrace);
                return decision == DealerAccessDecision.Allowed
                    ? TenantAccessDecision.Allow
                    : TenantAccessDecision.Deny(SuspendedError);
        }
    }

    private static bool IsRead(string method) =>
        string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);
}
