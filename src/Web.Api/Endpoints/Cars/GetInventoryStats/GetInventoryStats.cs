using Application.Queries.Cars.GetInventoryStats;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Cars.GetInventoryStats;

public sealed class GetInventoryStats : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/stats/inventory", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetInventoryStatsQuery(), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Cars)
        .WithName("GetInventoryStats")
        .Produces<InventoryStatsResponse>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}