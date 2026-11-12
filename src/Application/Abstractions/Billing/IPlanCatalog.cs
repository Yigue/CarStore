using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Billing.Entitlements;

namespace Application.Abstractions.Billing;

/// <summary>
/// spr PR 1.1 (SPR-02/BE-BILLING-07, ADR B1, D1): single source of truth for what is sellable today — the
/// landing page, checkout and provisioning all read the same catalog instead of three different hardcoded
/// prices. The Infrastructure implementation reads the live price from the payment provider and caches it.
/// </summary>
public interface IPlanCatalog
{
    /// <summary>Returns the plan's details, or null when <paramref name="code"/> is not sellable.</summary>
    Task<PlanDetails?> GetAsync(string code, CancellationToken ct = default);

    /// <summary>Returns every sellable plan (today: exactly one, per ADR B1).</summary>
    Task<IReadOnlyList<PlanDetails>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Pure, I/O-free sellability check (config lookup only — never calls the payment provider). Used to
    /// validate caller-supplied plan codes (e.g. provisioning) without making provider availability a
    /// precondition for a check that is really "is this one of our configured codes".
    /// </summary>
    bool IsSellable(string code);

    /// <summary>
    /// sfc A3.1 (A1, A21, RN-011): config-only lookup (no provider call) of the numeric limits declared
    /// for <paramref name="code"/> — <c>Billing:Plans:{code}:Limits:*</c>. Returns null when the code is
    /// not configured or declares no limits section at all: fail-open, same philosophy as
    /// <see cref="PlanEntitlementsEvaluator"/> (no limits row → unlimited, never an error).
    /// </summary>
    Task<PlanLimits?> GetEntitlementsAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// sfc A3.2 (A2, A24, RN-020, RN-021): config-only lookup of the feature flags included in
    /// <paramref name="code"/> — <c>Billing:Plans:{code}:Features</c>. Returns an empty list when the
    /// code is not configured or declares no features (fail-closed for feature gating: an unknown plan
    /// unlocks nothing, unlike the numeric limits which fail OPEN).
    /// </summary>
    Task<IReadOnlyList<string>> GetFeaturesAsync(string code, CancellationToken ct = default);
}

/// <summary>Provider-agnostic snapshot of a sellable plan. <see cref="AmountMinor"/> is in minor currency units.</summary>
public sealed record PlanDetails(
    string Code,
    string PriceId,
    long AmountMinor,
    string Currency,
    string Interval,
    int TrialDays);
