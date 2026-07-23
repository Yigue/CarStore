using Application.Users.Commands.CreateUser;
using Domain.Users;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Users;

internal sealed class CreateUser : IEndpoint
{
    public sealed record Request(
        string Email,
        string Password,
        string FirstName,
        string LastName,
        string? Phone,
        string Role);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("users", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(request.Role, out var role))
            {
                return Results.BadRequest(new { error = "Role must be a valid value: Admin, Empleado, Cliente, Invitado" });
            }

            var command = new CreateUserCommand(
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.Phone,
                role);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/users/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission("CanManageUsers")
        .WithTags(Tags.Users)
        .WithName("CreateUser")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}