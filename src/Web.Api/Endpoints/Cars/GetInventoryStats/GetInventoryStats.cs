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
            [Microsoft.AspNetCore.Mvc.FromQuery] string? groupBy,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetInventoryStatsQuery(GroupByBranch: string.Equals(groupBy, "branch", StringComparison.OrdinalIgnoreCase)), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .AllowAnonymous()
        .WithTags(Tags.Cars)
        .WithName("GetInventoryStats")
        .Produces<InventoryStatsResponse>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}