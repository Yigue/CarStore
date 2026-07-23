using Application.Users.Commands.UpdateUser;
using Domain.Users;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class UpdateUser : IEndpoint
{
    public sealed record Request(
        string FirstName,
        string LastName,
        string? Phone,
        string Role,
        bool IsActive);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("users/{userId:guid}", async (
            Guid userId,
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(request.Role, out var role))
            {
                return Results.BadRequest(new { error = "Role must be a valid value: Admin, Empleado, Cliente, Invitado" });
            }

            var command = new UpdateUserCommand(
                userId,
                request.FirstName,
                request.LastName,
                request.Phone,
                role,
                request.IsActive);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Ok(new { id }),
                CustomResults.Problem);
        })
        .HasPermission("CanManageUsers")
        .WithTags(Tags.Users)
        .WithName("UpdateUser")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}