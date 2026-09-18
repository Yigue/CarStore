using Application.Abstractions.Messaging;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Queries.GetPermissions;

/// <summary>
/// CFG-03: serves the real permission catalogue.
///
/// <para>
/// This used to return a hardcoded array of seven invented names — <c>CanManageInventory</c>,
/// <c>CanManageSales</c>, <c>CanViewReports</c> — that no endpoint in the API has ever checked.
/// A dealership could tick every box on the permissions screen and end up with a user who still
/// could not open the inventory, because nothing the screen offered corresponded to anything the
/// rules ask for. <see cref="PermissionCatalog"/> is that correspondence, and an architecture test
/// fails the build if an endpoint ever guards itself with something the catalogue omits.
/// </para>
/// </summary>
internal sealed class GetPermissionsQueryHandler : IQueryHandler<GetPermissionsQuery, PermissionsResponse>
{
    public Task<Result<PermissionsResponse>> Handle(GetPermissionsQuery query, CancellationToken cancellationToken)
    {
        var permissions = PermissionCatalog.All
            .Select(p => new PermissionResponse(p.Value, p.Label, p.Module))
            .ToArray();

        return Task.FromResult(Result.Success(new PermissionsResponse(permissions)));
    }
}
