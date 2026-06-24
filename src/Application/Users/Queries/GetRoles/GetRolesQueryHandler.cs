using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Users.Queries.GetRoles;

internal sealed class GetRolesQueryHandler : IQueryHandler<GetRolesQuery, RolesResponse>
{
    public Task<Result<RolesResponse>> Handle(GetRolesQuery query, CancellationToken cancellationToken)
    {
        var roles = new[]
        {
            new RoleResponse("Admin", "Administrador"),
            new RoleResponse("Empleado", "Empleado"),
            new RoleResponse("Cliente", "Cliente"),
            new RoleResponse("Invitado", "Invitado")
        };

        return Task.FromResult(Result.Success(new RolesResponse(roles)));
    }
}