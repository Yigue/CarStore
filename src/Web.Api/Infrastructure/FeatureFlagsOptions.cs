namespace Web.Api.Infrastructure;

/// <summary>
/// Feature flags for the CRM hardening rollout (ADR-6).
/// Bind from appsettings.json section "FeatureFlags".
/// All flags default to <c>false</c>; each PR activates the relevant flag.
/// </summary>
public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";

    /// <summary>PR1: When true, BE enforces strict PascalCase enum strings on the wire.
    /// When false (default), BE accepts both numeric and string forms (dual-accept window).</summary>
    public bool CrmEnumV2 { get; set; } = false;

    /// <summary>PR1: When true, clients soft-delete query filter is active.
    /// Defaults true (safe — only changes DELETE semantics; GET is unaffected).</summary>
    public bool CrmSoftDelete { get; set; } = true;

    /// <summary>PR3: When true, bulk CSV export endpoint is enabled.</summary>
    public bool CrmBulkExport { get; set; } = false;

    /// <summary>PR2: When true, activity timeline endpoint is active.</summary>
    public bool CrmActivityTl { get; set; } = false;

    /// <summary>PR2: When true, client notes PUT endpoint requires the updated notes contract.</summary>
    public bool CrmClientNotes { get; set; } = false;

    /// <summary>PR3: When true, subscription payment status is enforced.</summary>
    public bool SubscriptionEnforcement { get; set; } = false;
}
