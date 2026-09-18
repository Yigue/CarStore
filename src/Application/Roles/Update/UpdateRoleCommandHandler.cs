using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Update;

internal sealed class UpdateRoleCommandHandler(
    IApplicationDbContext context,
    IPermissionCacheInvalidator permissionCacheInvalidator)
    : ICommandHandler<UpdateRoleCommand>
{
    /// <summary>The permission that reaches this very screen. Losing the last of it is a one-way door.</summary>
    private const string RoleAdministration = "CanManageRoles";

    public async Task<Result> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        Role? role = await context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleId));
        }

        string name = command.Name.Trim();

        bool nameTaken = await context.Roles
            .AnyAsync(r => r.Name == name && r.Id != role.Id, cancellationToken);

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
            bool anotherAdministratorExists = await context.RolePermissions
                .AnyAsync(
                    rp => rp.Permission == RoleAdministration && rp.RoleId != role.Id,
                    cancellationToken);

            if (!anotherAdministratorExists)
            {
                return Result.Failure(RoleErrors.WouldRemoveLastAdministrator());
            }
        }

        role.Rename(name, command.Description?.Trim() ?? string.Empty);
        role.ReplacePermissions(requested);

        await context.SaveChangesAsync(cancellationToken);

        // What this role may do just changed for everyone holding it. Without this the change
        // waits out a thirty-minute cache — and a REVOKED permission keeps working meanwhile.
        await permissionCacheInvalidator.InvalidateRoleAsync(role.Id, cancellationToken);

        return Result.Success();
    }
}
