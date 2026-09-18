using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Queries.GetRoles;

internal sealed class GetRolesQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetRolesQuery, RolesResponse>
{
    public async Task<Result<RolesResponse>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
    {
        // Roles are per-dealership, not shared — Role.SetDealer stamps each row with the
        // creating tenant. Returning a hardcoded {"Admin","Empleado",...} array used to put the
        // role's NAME in the "id" field, which no caller could ever turn into the GUID
        // CreateUserCommandHandler expects. This is the real table, scoped to the caller.
        var roles = await context.Roles
            .Where(r => r.DealerId == tenantService.DealerId)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                Permissions = r.Permissions.Select(p => p.Permission).ToList(),
                // Counted in SQL rather than by loading the users: a dealership with two hundred
                // people would otherwise pull all of them to render a number.
                UserCount = context.Users.Count(u => u.RoleId == r.Id),
            })
            .ToListAsync(cancellationToken);

        var response = roles
            .Select(r => new RoleResponse(
                r.Id.ToString(),
                r.Name,
                r.Description ?? string.Empty,
                r.Permissions.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
                r.UserCount))
            .ToArray();

        return Result.Success(new RolesResponse(response));
    }
}
