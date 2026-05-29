using Application.Users.Commands.DeleteUser;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class DeleteUser : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("users/{userId:guid}", async (
            Guid userId,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new DeleteUserCommand(userId);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                _ => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission("CanManageUsers")
        .WithTags(Tags.Users)
        .WithName("DeleteUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}