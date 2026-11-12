using Application.Abstractions.Authentication;
using Application.Abstractions.Branches;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

/// <summary>
/// sfc C2.2 / ADR C26: admins and roles without <c>branch:scoped</c> see the whole dealer; a scoped user sees only the
/// branches assigned in <c>user_branches</c> (none assigned means nothing visible, never everything).
/// </summary>
internal sealed class BranchScope(
    IUserContext userContext,
    PermissionProvider permissions,
    IApplicationDbContext context,
    ICurrentTenantService tenantService) : IBranchScope
{
    public const string ScopedPermission = "branch:scoped";

    public async Task<IReadOnlyCollection<Guid>?> GetRestrictedBranchIdsAsync(CancellationToken cancellationToken)
    {
        if (!userContext.IsAuthenticated || userContext.IsAdmin)
            return null;

        // sfc B6.3: the permission this user holds depends on which dealer their active session is scoped to.
        HashSet<string> granted = await permissions.GetForUserIdAsync(userContext.UserId, tenantService.DealerId);
        if (!granted.Contains(ScopedPermission))
            return null;

        Guid userId = userContext.UserId;
        return await context.UserBranches
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.BranchId)
            .ToListAsync(cancellationToken);
    }
}
