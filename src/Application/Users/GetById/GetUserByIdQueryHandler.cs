using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.GetById;

internal sealed class GetUserByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        // Get user with tenant isolation - must belong to current dealer
        var user = await context.Users
            .Where(u => u.Id == query.UserId && u.DealerId == tenantService.DealerId)
            .Select(u => new UserResponse
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Phone = u.Phone,
                RoleId = u.RoleId,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound(query.UserId));
        }

        // spr PR 4.2 / H9 follow-up: effective permissions come exclusively from the user's Role ->
        // RolePermission, never the legacy UserPermissions table (its write path was deleted in PR 4.2;
        // the navigation used here before always read back an empty collection for a tenant user).
        List<string> permissions = await context.RolePermissions
            .Where(rp => rp.RoleId == user.RoleId)
            .Select(rp => rp.Permission)
            .ToListAsync(cancellationToken);

        return user with { Permissions = permissions };
    }
}
