using Application.Queries.Clients.GetTop;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Clients.GetTop;

public sealed class GetTopClients : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/top", async (
            [FromQuery] int limit,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new GetTopClientsQuery { Limit = limit };
            var result = await sender.Send(query, cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Clients)
        .WithName("GetTopClients")
        .Produces<IEnumerable<ClientWithRevenueResponse>>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}