using Application.Users.Queries.GetAllUsers;
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
        // GET /api/users - List all users with pagination
        app.MapGet("users", async (
            int page,
            int pageSize,
            string? search,
            string? role,
            bool? isActive,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Domain.Users.UserRole? roleFilter = null;
            if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<Domain.Users.UserRole>(role, true, out var parsedRole))
            {
                roleFilter = parsedRole;
            }

            var query = new GetAllUsersQuery(page, pageSize, search, roleFilter, isActive);

            var result = await sender.Send(query, cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .HasPermission("CanManageUsers")
        .WithTags(Tags.Users)
        .WithName("GetUserDetails")
        .Produces<PaginatedUsersResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

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
        .WithTags(Tags.Users)
        .WithName("GetRoles")
        .Produces<RoleResponse>()
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
        .WithTags(Tags.Users)
        .WithName("GetPermissions")
        .Produces<PermissionResponse>()
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