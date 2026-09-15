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
            .Select(r => new RoleResponse(r.Id.ToString(), r.Name))
            .ToListAsync(cancellationToken);

        return Result.Success(new RolesResponse(roles));
    }
}
