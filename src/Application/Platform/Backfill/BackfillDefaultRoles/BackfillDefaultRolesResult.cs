using Domain.Cars;

namespace Application.Platform.Backfill.BackfillDefaultRoles;

/// <summary>One default role's permission drift for one dealer.</summary>
public sealed record RolePermissionDiff(
    string RoleName,
    IReadOnlyList<string> MissingPermissions,
    IReadOnlyList<string> ExtraPermissions);

/// <summary>One dealer's full drift report: default roles it is missing entirely, plus
/// permission drift on the default roles it already has.</summary>
public sealed record DealerRoleDiff(
    Guid DealerId,
    IReadOnlyList<string> MissingRoles,
    IReadOnlyList<RolePermissionDiff> RolePermissionDiffs);

/// <summary>
/// Result of the default-role backfill. <see cref="Diffs"/> only lists dealers that actually
/// had a difference — a dealer already converged with the catalogue is simply absent, so an
/// idempotent re-run (dry-run or confirmed) reports an empty list.
/// </summary>
public sealed record BackfillDefaultRolesResult(
    BackfillAction Action,
    int DealersAffected,
    IReadOnlyList<DealerRoleDiff> Diffs);
