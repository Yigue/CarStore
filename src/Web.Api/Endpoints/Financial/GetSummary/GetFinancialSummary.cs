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
            [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? from,
            [Microsoft.AspNetCore.Mvc.FromQuery] DateTime? to,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetFinancialSummaryQuery(from?.ToUtc(), to?.ToUtc()), cancellationToken);

            return result.Match(
                data => Results.Ok(data),
                CustomResults.Problem);
        })
        .HasPermission("financial:read")
        .WithTags(Tags.Financial)
        .WithName("GetFinancialSummary")
        .Produces<FinancialSummaryResponse>()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}