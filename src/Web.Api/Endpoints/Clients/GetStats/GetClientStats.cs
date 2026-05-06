using Application.Queries.Clients.GetStats;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Clients.GetStats;

public sealed class GetClientStats : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("clients/stats", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetClientStatsQuery(), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Clients)
        .WithName("GetClientStats")
        .Produces<ClientStatsResponse>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}