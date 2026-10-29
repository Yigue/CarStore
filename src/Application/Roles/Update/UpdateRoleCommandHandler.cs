using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Roles.Update;

internal sealed class UpdateRoleCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    ICurrentTenantService tenantService,
    IPermissionCacheInvalidator permissionCacheInvalidator,
    IEntitlementService entitlementService)
    : ICommandHandler<UpdateRoleCommand>
{
    /// <summary>The permission that reaches this very screen. Losing the last of it is a one-way door.</summary>
    private const string RoleAdministration = "CanManageRoles";

    public async Task<Result> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        // Security review (sfc A3.1): explicit scoping on top of Role's global query filter
        // (defense in depth — this is exactly the check that filter's absence let slip through).
        Role? role = await context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == command.RoleId && r.DealerId == tenantService.DealerId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleId));
        }

        string name = command.Name.Trim();

        bool nameTaken = await context.Roles
            .AnyAsync(r => r.Name == name && r.Id != role.Id && r.DealerId == tenantService.DealerId, cancellationToken);

        if (nameTaken)
        {
            return Result.Failure(RoleErrors.NameAlreadyExists(name));
        }

        var requested = command.Permissions
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (string permission in requested)
        {
            if (!PermissionCatalog.IsGrantable(permission))
            {
                return Result.Failure(RoleErrors.PermissionNotGrantable(permission));
            }
        }

        // ── The lock-out guard ───────────────────────────────────────────────────────────────
        //
        // Taking CanManageRoles off the LAST role that has it closes the door from the inside:
        // no screen anyone can still reach is able to put it back, and recovery means someone
        // with database access. Checked before writing, and only when this role is actually
        // giving it up.
        bool wasAdministrator = role.Permissions.Any(p => p.Permission == RoleAdministration);
        bool staysAdministrator = requested.Contains(RoleAdministration, StringComparer.Ordinal);

        if (wasAdministrator && !staysAdministrator)
        {
            // Security review (sfc A3.1): RolePermissions carries no DealerId of its own — an
            // unscoped check here would ask "does CanManageRoles exist on ANY role of ANY dealer",
            // which is true on any populated system and makes this guard pass even when this role
            // really is the last one with it IN THIS dealer. Join back to Roles to scope it.
            bool anotherAdministratorExists = await context.Roles
                .Where(r => r.DealerId == tenantService.DealerId && r.Id != role.Id)
                .Join(context.RolePermissions, r => r.Id, rp => rp.RoleId, (r, rp) => rp.Permission)
                .AnyAsync(p => p == RoleAdministration, cancellationToken);

            if (!anotherAdministratorExists)
            {
                return Result.Failure(RoleErrors.WouldRemoveLastAdministrator());
            }
        }

        // Security review (sfc A3.1), third round: a role gaining its FIRST permission promotes every
        // existing holder to a seat in one step — nobody's own RoleId changes, so AssignRole/UpdateUser's
        // per-user gate never sees it. Only the zero-to-some transition matters; a role that already had
        // permissions can have them edited freely (its holders were already counted).
        bool roleCurrentlyHasNoPermissions = role.Permissions.Count == 0;
        bool wouldGainItsFirstPermission = roleCurrentlyHasNoPermissions && requested.Count > 0;

        IDbContextTransaction? transaction = wouldGainItsFirstPermission && dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            if (wouldGainItsFirstPermission)
            {
                Result entitlementCheck = await entitlementService.EnsureRoleCanGainHoldersAsync(
                    tenantService.DealerId, role.Id, cancellationToken);
                if (entitlementCheck.IsFailure)
                {
                    if (transaction is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }

                    return Result.Failure(entitlementCheck.Error);
                }
            }

            role.Rename(name, command.Description?.Trim() ?? string.Empty);
            role.ReplacePermissions(requested);

            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        // What this role may do just changed for everyone holding it. Without this the change
        // waits out a thirty-minute cache — and a REVOKED permission keeps working meanwhile.
        await permissionCacheInvalidator.InvalidateRoleAsync(role.Id, cancellationToken);

        return Result.Success();
    }
}
