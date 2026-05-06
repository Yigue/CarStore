using Application.Queries.Sales.GetToday;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Sales.GetToday;

public sealed class GetTodaySales : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("sales/today", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetTodaySalesQuery(), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Sales)
        .WithName("GetTodaySales")
        .Produces<TodaySalesResponse>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}