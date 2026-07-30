using Application.Users.Queries.GetAllUsers;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Users.GetAll;

public sealed class GetAllUsers : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("users", async (
            int? page,
            int? pageSize,
            string? search,
            string? role,
            bool? isActive,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            // Parse role if provided
            Guid? roleFilter = null;
            if (!string.IsNullOrWhiteSpace(role) && Guid.TryParse(role, out var parsedRole))
            {
                roleFilter = parsedRole;
            }

            var query = new GetAllUsersQuery(page ?? 1, pageSize ?? 20, search, roleFilter, isActive);

            var result = await sender.Send(query, cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .HasPermission("CanManageUsers")
        .WithTags(Tags.Users)
        .WithName("GetAllUsers")
        .Produces<PaginatedUsersResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}