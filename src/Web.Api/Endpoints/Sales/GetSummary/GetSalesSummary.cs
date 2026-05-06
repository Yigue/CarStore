using Application.Queries.Sales.GetSummary;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Sales.GetSummary;

public sealed class GetSalesSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("sales/summary", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetSalesSummaryQuery(), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Sales)
        .WithName("GetSalesSummary")
        .Produces<SalesSummaryResponse>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}