using System.Diagnostics;
using System.Text.Json;
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
/// converges it (confirmed) — creating missing roles and fully syncing existing ones
/// (<see cref="DefaultRoleCatalog.Sync"/> adds missing permissions AND removes extras, unlike
/// the additive-only seed-time behavior in <c>UsersSeeder</c>, per design.md Fase 4).
///
/// <para>
/// Platform-wide, not tenant-scoped: iterates every <c>DealerSettings</c> row regardless of the
/// caller's own tenant. One <see cref="BackfillAudit"/> row is appended per dealer that actually
/// had a difference (dealers already converged write nothing — the audit table would otherwise
/// grow by one row per dealer on every no-op re-run).
/// </para>
/// </summary>
internal sealed class BackfillDefaultRolesCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
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
                        context.Roles.Add(DefaultRoleCatalog.BuildRole(dealerId, roleName));
                    }

                    continue;
                }

                (IReadOnlyList<string> missing, IReadOnlyList<string> extra) = DefaultRoleCatalog.Diff(role);
                if (missing.Count == 0 && extra.Count == 0)
                {
                    continue;
                }

                rolePermissionDiffs.Add(new RolePermissionDiff(roleName, missing, extra));
                if (!command.DryRun)
                {
                    DefaultRoleCatalog.Sync(role);
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
