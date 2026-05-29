using Application.Users.Commands.GrantPermissions;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class UpdatePermissions : IEndpoint
{
    public sealed record Request(IEnumerable<string> Permissions);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/{userId:guid}/permissions", async (
            Guid userId,
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new GrantPermissionsCommand(userId, request.Permissions);

            Result<GrantPermissionsResult> result = await sender.Send(command, cancellationToken);

            return result.Match(
                data => Results.Ok(new { granted = data.Granted, revoked = data.Revoked }),
                CustomResults.Problem);
        })
        .HasPermission("CanManageRoles")
        .WithTags(Tags.Users)
        .WithName("UpdateUserPermissions")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}