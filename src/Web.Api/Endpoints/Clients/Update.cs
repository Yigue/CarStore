using Application.Clients.Update;
using Domain.Clients.Attributes;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Clients;

internal sealed class Update : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("clients/{id}", async (Guid id, UpdateClientRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateClientCommand(
                id,
                request.FirstName,
                request.LastName,
                request.DNI,
                request.Email,
                request.Phone,
                request.Address,
                request.Status);

            Result result = await sender.Send(command, cancellationToken);

            return result.Match(
                () => Results.NoContent(),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.ClientsUpdate)
        .WithTags(Tags.Clients)
        .WithName("UpdateClient")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }   
}

public sealed record UpdateClientRequest(
    string FirstName,
    string LastName,
    string DNI,
    string Email,
    string Phone,
    string Address,
    ClientStatus Status);
