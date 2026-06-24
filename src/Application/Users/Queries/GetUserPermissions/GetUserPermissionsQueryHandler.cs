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
        // Verify user belongs to the current tenant
        var userExists = await context.Users
            .AnyAsync(u => u.Id == query.UserId && u.DealerId == tenantService.DealerId, cancellationToken);

        if (!userExists)
        {
            return Result.Failure<UserPermissionsResponse>(Domain.Users.UserErrors.NotFound(query.UserId));
        }

        var permissions = await context.UserPermissions
            .Where(up => up.UserId == query.UserId)
            .Select(up => up.Permission)
            .ToListAsync(cancellationToken);

        return Result.Success(new UserPermissionsResponse(query.UserId, permissions));
    }
}