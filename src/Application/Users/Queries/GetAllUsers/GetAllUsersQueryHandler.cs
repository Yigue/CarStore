using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Queries.GetAllUsers;

internal sealed class GetAllUsersQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetAllUsersQuery, PaginatedUsersResponse>
{
    public async Task<Result<PaginatedUsersResponse>> Handle(GetAllUsersQuery query, CancellationToken cancellationToken)
    {
        var dealerId = tenantService.DealerId;

        var usersQuery = context.Users
            .Where(u => u.DealerId == dealerId);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchLower = query.Search.ToLower();
            usersQuery = usersQuery.Where(u =>
                u.FirstName.ToLower().Contains(searchLower) ||
                u.LastName.ToLower().Contains(searchLower) ||
                u.Email.Value.ToLower().Contains(searchLower));
        }

        if (query.RoleId.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.RoleId == query.RoleId.Value);
        }

        if (query.IsActive.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        var total = await usersQuery.CountAsync(cancellationToken);

        var users = await usersQuery
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UserListResponse(
                u.Id,
                u.Email.Value,
                u.FirstName,
                u.LastName,
                u.Phone,
                u.RoleId.ToString(),
                u.IsActive,
                u.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new PaginatedUsersResponse(users, total, query.Page, query.PageSize));
    }
}