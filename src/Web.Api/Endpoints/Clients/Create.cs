using Application.Clients.Create;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Clients;

internal sealed class Create : IEndpoint
{
    public sealed record Request(
        string FirstName,
        string LastName,
        string Email,
        string Phone,
        string Address,
        string DNI);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("clients", async (Request request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new CreateClientCommand(
                request.FirstName,
                request.LastName,
                request.Email,
                request.Phone,
                request.Address,
                request.DNI);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/clients/{id}", new { id }),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.ClientsCreate)
        .WithTags(Tags.Clients)
        .WithName("CreateClient")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
