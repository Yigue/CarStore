using Application.Users.Commands.AssignRole;
using Domain.Users;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class AssignRole : IEndpoint
{
    public sealed record Request(string Role);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users/{userId:guid}/role", async (
            Guid userId,
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(request.Role, out var role))
            {
                return Results.BadRequest(new { error = "Role must be a valid value: Admin, Empleado, Cliente, Invitado" });
            }

            var command = new AssignRoleCommand(userId, role);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Ok(new { id }),
                CustomResults.Problem);
        })
        .HasPermission("CanManageRoles")
        .WithTags(Tags.Users)
        .WithName("AssignUserRole")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}