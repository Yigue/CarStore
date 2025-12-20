using Application.Clients.GetAll;
using Application.Clients.GetById;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Clients;

internal sealed class GetById : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/{id}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetClientByIdQuery(id);

            Result<ClientResponse> result = await sender.Send(query, cancellationToken);

            return result.Match(
                client => Results.Ok(client),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.ClientsRead)
        .WithTags(Tags.Clients)
        .WithName("GetClientById")
        .Produces<ClientResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
