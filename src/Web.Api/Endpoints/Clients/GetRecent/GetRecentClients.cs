using Application.Clients.GetAll;
using Application.Queries.Clients.GetRecent;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Clients.GetRecent;

public sealed class GetRecentClients : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/recent", async (
            [FromQuery] int limit,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetRecentClientsQuery { Limit = limit };
            var result = await sender.Send(query, cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Clients)
        .WithName("GetRecentClients")
        .Produces<IEnumerable<ClientResponse>>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}