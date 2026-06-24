using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Users.Queries.GetPermissions;

internal sealed class GetPermissionsQueryHandler : IQueryHandler<GetPermissionsQuery, PermissionsResponse>
{
    public Task<Result<PermissionsResponse>> Handle(GetPermissionsQuery query, CancellationToken cancellationToken)
    {
        var permissions = new[]
        {
            new PermissionResponse("CanManageUsers", "Gestionar Usuarios"),
            new PermissionResponse("CanManageRoles", "Gestionar Roles"),
            new PermissionResponse("CanManageInventory", "Gestionar Inventario"),
            new PermissionResponse("CanManageSales", "Gestionar Ventas"),
            new PermissionResponse("CanManageFinance", "Gestionar Finanzas"),
            new PermissionResponse("CanManageLeads", "Gestionar Leads"),
            new PermissionResponse("CanViewReports", "Ver Reportes")
        };

        return Task.FromResult(Result.Success(new PermissionsResponse(permissions)));
    }
}