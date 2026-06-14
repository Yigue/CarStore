using Application.Queries.Clients.GetIncomplete;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Clients.GetIncomplete;

public sealed class GetIncomplete : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/incomplete", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetIncompleteClientsQuery();
            var result = await sender.Send(query, cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .HasPermission("clients:read")
        .WithTags(Tags.Clients)
        .WithName("GetIncompleteClients")
        .Produces<IEnumerable<IncompleteClientResponse>>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
