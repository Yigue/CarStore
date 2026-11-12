using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Delete;

internal sealed class DeleteRoleCommandHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : ICommandHandler<DeleteRoleCommand>
{
    private const string RoleAdministration = "CanManageRoles";

    public async Task<Result> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        // Security review (sfc A3.1): explicit scoping on top of Role's global query filter
        // (defense in depth — this is the same cross-tenant gap UpdateRoleCommandHandler had).
        Role? role = await context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == command.RoleId && r.DealerId == tenantService.DealerId, cancellationToken);

        if (role is null)
        {
            return Result.Failure(RoleErrors.NotFound(command.RoleId));
        }

        // Deleting a role out from under its holders leaves them with no permissions at all:
        // locked out of a system they were using a second ago, with nothing saying why. Whoever
        // deletes it has to say where those people go first.
        int assignedUsers = await context.Users
            .CountAsync(u => u.RoleId == role.Id, cancellationToken);

        if (assignedUsers > 0)
        {
            return Result.Failure(RoleErrors.StillAssigned(role.Id, assignedUsers));
        }

        // Same one-way door as UpdateRoleCommandHandler: an unassigned role can still be the only
        // one carrying CanManageRoles, and deleting it would leave nobody able to create another.
        // Scoped the same way — RolePermissions carries no DealerId, so an unscoped check would ask
        // "does CanManageRoles exist anywhere on the platform", true on any populated system.
        if (role.Permissions.Any(p => p.Permission == RoleAdministration))
        {
            bool anotherAdministratorExists = await context.Roles
                .Where(r => r.DealerId == tenantService.DealerId && r.Id != role.Id)
                .Join(context.RolePermissions, r => r.Id, rp => rp.RoleId, (r, rp) => rp.Permission)
                .AnyAsync(p => p == RoleAdministration, cancellationToken);

            if (!anotherAdministratorExists)
            {
                return Result.Failure(RoleErrors.WouldRemoveLastAdministrator());
            }
        }

        context.Roles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
