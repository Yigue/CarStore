using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Roles.Delete;

internal sealed class DeleteRoleCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteRoleCommand>
{
    private const string RoleAdministration = "CanManageRoles";

    public async Task<Result> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        Role? role = await context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == command.RoleId, cancellationToken);

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
        if (role.Permissions.Any(p => p.Permission == RoleAdministration))
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

        context.Roles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
