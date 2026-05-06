using Application.Queries.Financial.GetSummary;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;
using Web.Api.Endpoints;

namespace Web.Api.Endpoints.Financial.GetSummary;

public sealed class GetFinancialSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("financial/summary", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetFinancialSummaryQuery(), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Financial)
        .WithName("GetFinancialSummary")
        .Produces<FinancialSummaryResponse>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}