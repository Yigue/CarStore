using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Queries.GetUserPermissions;

internal sealed class GetUserPermissionsQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetUserPermissionsQuery, UserPermissionsResponse>
{
    public async Task<Result<UserPermissionsResponse>> Handle(GetUserPermissionsQuery query, CancellationToken cancellationToken)
    {
        // Verify user belongs to the current tenant, and read its RoleId in the same query.
        var roleId = await context.Users
            .Where(u => u.Id == query.UserId && u.DealerId == tenantService.DealerId)
            .Select(u => (Guid?)u.RoleId)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleId is null)
        {
            return Result.Failure<UserPermissionsResponse>(Domain.Users.UserErrors.NotFound(query.UserId));
        }

        // spr PR 4.2 / H9: effective permissions come exclusively from the user's Role ->
        // RolePermission, never from the legacy UserPermissions grant table (deleted in this PR).
        var permissions = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId.Value)
            .Select(rp => rp.Permission)
            .ToListAsync(cancellationToken);

        return Result.Success(new UserPermissionsResponse(query.UserId, permissions));
    }
}