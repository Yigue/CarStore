using Application.Dashboard.GetDashboardSummary;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Dashboard;

internal sealed class GetSummary : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("dashboard/summary", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Result<DashboardSummaryDto> result =
                await sender.Send(new GetDashboardSummaryQuery(), cancellationToken);

            return result.Match(
                summary => Results.Ok(summary),
                CustomResults.Problem);
        })
        .RequireAuthorization()
        .WithTags(Tags.Dashboard)
        .WithName("GetDashboardSummary")
        .Produces<DashboardSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
