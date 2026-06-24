using Application.Users.Queries.GetUserPermissions;
using Application.Users.Queries.GetRoles;
using Application.Users.Queries.GetPermissions;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Users;

public sealed class GetUserDetails : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/roles - List all roles
        app.MapGet("roles", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetRolesQuery(), cancellationToken);

            return result.Match(
                data => Results.Ok(data.Roles),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CanManageRoles)
        .WithTags(Tags.Users)
        .WithName("GetRoles")
        .Produces<RoleResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET /api/permissions - List all permissions
        app.MapGet("permissions", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPermissionsQuery(), cancellationToken);

            return result.Match(
                data => Results.Ok(data.Permissions),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.CanManageRoles)
        .WithTags(Tags.Users)
        .WithName("GetPermissions")
        .Produces<PermissionResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        // GET /api/users/{userId}/permissions - Get user permissions
        app.MapGet("users/{userId:guid}/permissions", async (
            Guid userId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetUserPermissionsQuery(userId), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .HasPermission("CanManageUsers")
        .WithTags(Tags.Users)
        .WithName("GetUserPermissions")
        .Produces<UserPermissionsResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}