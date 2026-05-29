using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Commands.GrantPermissions;

internal sealed class GrantPermissionsCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IUserContext userContext)
    : ICommandHandler<GrantPermissionsCommand, GrantPermissionsResult>
{
    public async Task<Result<GrantPermissionsResult>> Handle(GrantPermissionsCommand command, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId && u.DealerId == tenantService.DealerId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<GrantPermissionsResult>(UserErrors.NotFound(command.UserId));
        }

        // Prevent self-revocation of CanManageUsers if user is Admin
        if (command.UserId == userContext.UserId)
        {
            var currentPermissions = await context.UserPermissions
                .Where(up => up.UserId == userContext.UserId)
                .Select(up => up.Permission)
                .ToListAsync(cancellationToken);

            bool hasCanManageUsers = currentPermissions.Contains(Permissions.CanManageUsers);
            bool willLoseCanManageUsers = !command.Permissions.Contains(Permissions.CanManageUsers) && hasCanManageUsers;

            if (willLoseCanManageUsers)
            {
                return Result.Failure<GrantPermissionsResult>(UserErrors.CannotRevokeOwnPermission);
            }
        }

        // Get current permissions for this user
        var currentUserPermissions = await context.UserPermissions
            .Where(up => up.UserId == command.UserId)
            .ToListAsync(cancellationToken);

        var requestedPermissions = command.Permissions.ToList();
        var currentPermissionSet = currentUserPermissions.Select(p => p.Permission).ToHashSet();

        var toGrant = requestedPermissions.Except(currentPermissionSet).ToList();
        var toRevoke = currentUserPermissions.Where(p => !requestedPermissions.Contains(p.Permission)).ToList();

        // Grant new permissions
        foreach (var permission in toGrant)
        {
            var userPermission = new UserPermission(command.UserId, permission, userContext.UserId);
            context.UserPermissions.Add(userPermission);
        }

        // Revoke removed permissions
        context.UserPermissions.RemoveRange(toRevoke);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(new GrantPermissionsResult(toGrant, toRevoke.Select(p => p.Permission)));
    }
}

internal static class Permissions
{
    internal const string CanManageUsers = "CanManageUsers";
    internal const string CanManageRoles = "CanManageRoles";
    internal const string CanManageInventory = "CanManageInventory";
    internal const string CanManageSales = "CanManageSales";
    internal const string CanManageFinance = "CanManageFinance";
    internal const string CanManageLeads = "CanManageLeads";
    internal const string CanViewReports = "CanViewReports";
}