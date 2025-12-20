using Application.Clients.GetAll;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Clients;

internal sealed class Get : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var query = new GetAllClientsQuery();

            Result<IReadOnlyList<ClientResponse>> result = await sender.Send(query, cancellationToken);

            return result.Match(
                clients => Results.Ok(clients),
                CustomResults.Problem);
        })
        .HasPermission(Permissions.ClientsRead)
        .WithTags(Tags.Clients)
        .WithName("GetAllClients")
        .Produces<IReadOnlyList<ClientResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
