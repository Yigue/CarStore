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

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            return Results.Ok(result.Value);
        })
        .WithTags("Clients");
    }
}
