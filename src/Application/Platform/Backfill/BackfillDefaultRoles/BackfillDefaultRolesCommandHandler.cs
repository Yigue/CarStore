using System.Diagnostics;
using System.Text.Json;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Platform.Backfill.BackfillDefaultRoles;

/// <summary>
/// spr PR 4.1 (SPR-18, D6): walks every dealer, compares its Admin/Empleado/Cliente roles
/// against <see cref="DefaultRoleCatalog"/>, and either reports the drift (<c>DryRun</c>) or
/// closes it (confirmed) — creating any missing role from the catalogue and adding any
/// permission an existing one is missing.
///
/// <para>
/// Security review: it does NOT remove a permission the catalogue does not list. A role named
/// exactly Admin/Empleado/Cliente can be a dealer's own customization of that role through the
/// normal role-editing screen — this job cannot tell "drifted by the seeding bug this PR fixes"
/// apart from "a dealer deliberately added a permission afterward", so an unattended,
/// platform-wide run must never delete one. <see cref="DefaultRoleCatalog.Sync"/> (full
/// add-and-remove) stays available as a primitive for a human to apply to one reviewed role; this
/// handler only ever adds. Any extra permission is still reported in the diff, in both modes, for
/// a person to act on deliberately.
/// </para>
///
/// <para>
/// Platform-wide, not tenant-scoped: iterates every <c>DealerSettings</c> row regardless of the
/// caller's own tenant. One <see cref="BackfillAudit"/> row is appended per dealer that actually
/// had a difference (dealers already converged write nothing — the audit table would otherwise
/// grow by one row per dealer on every no-op re-run). Every role created or topped up has its
/// holders' cached permissions invalidated, the same way <c>UpdateRoleCommandHandler</c> does for
/// a manual edit — otherwise the fix sits unused for up to the cache's TTL.
/// </para>
/// </summary>
internal sealed class BackfillDefaultRolesCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IPermissionCacheInvalidator permissionCacheInvalidator)
    : ICommandHandler<BackfillDefaultRolesCommand, BackfillDefaultRolesResult>
{
    public async Task<Result<BackfillDefaultRolesResult>> Handle(
        BackfillDefaultRolesCommand command,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        BackfillAction action = command.DryRun ? BackfillAction.DryRun : BackfillAction.Apply;

        List<Guid> dealerIds = await context.DealerSettings
            .IgnoreQueryFilters()
            .Select(d => d.DealerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var diffs = new List<DealerRoleDiff>();
        var touchedRoleIds = new List<Guid>();

        foreach (Guid dealerId in dealerIds)
        {
            List<Role> existingRoles = await context.Roles
                .IgnoreQueryFilters()
                .Include(r => r.Permissions)
                .Where(r => r.DealerId == dealerId)
                .ToListAsync(cancellationToken);

            var missingRoles = new List<string>();
            var rolePermissionDiffs = new List<RolePermissionDiff>();

            foreach (string roleName in DefaultRoleCatalog.RoleNames)
            {
                Role? role = existingRoles.Find(r => r.Name == roleName);

                if (role is null)
                {
                    missingRoles.Add(roleName);
                    if (!command.DryRun)
                    {
                        var created = DefaultRoleCatalog.BuildRole(dealerId, roleName);
                        context.Roles.Add(created);
                        touchedRoleIds.Add(created.Id);
                    }

                    continue;
                }

                (IReadOnlyList<string> missing, IReadOnlyList<string> extra) = DefaultRoleCatalog.Diff(role);
                if (missing.Count == 0 && extra.Count == 0)
                {
                    continue;
                }

                rolePermissionDiffs.Add(new RolePermissionDiff(roleName, missing, extra));
                if (!command.DryRun && missing.Count > 0)
                {
                    // Additive only — see the class remarks. `extra` is reported above but never removed here.
                    foreach (string permission in missing)
                    {
                        role.AddPermission(permission);
                    }

                    touchedRoleIds.Add(role.Id);
                }
            }

            if (missingRoles.Count > 0 || rolePermissionDiffs.Count > 0)
            {
                diffs.Add(new DealerRoleDiff(dealerId, missingRoles, rolePermissionDiffs));
            }
        }

        if (!command.DryRun && diffs.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);

            foreach (Guid roleId in touchedRoleIds)
            {
                await permissionCacheInvalidator.InvalidateRoleAsync(roleId, cancellationToken);
            }
        }

        stopwatch.Stop();

        if (diffs.Count > 0)
        {
            foreach (DealerRoleDiff diff in diffs)
            {
                int affectedCount = diff.MissingRoles.Count
                    + diff.RolePermissionDiffs.Sum(d => d.MissingPermissions.Count + d.ExtraPermissions.Count);

                BackfillAudit audit = BackfillAudit.Create(
                    diff.DealerId,
                    command.ActorUserId,
                    action,
                    affectedCount,
                    (int)stopwatch.ElapsedMilliseconds,
                    JsonSerializer.Serialize(diff),
                    dateTimeProvider.UtcNow);

                context.BackfillAudits.Add(audit);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new BackfillDefaultRolesResult(action, diffs.Count, diffs));
    }
}
