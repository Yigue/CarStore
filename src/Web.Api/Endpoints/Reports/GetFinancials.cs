using Application.Reports.GetFinancialReport;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Reports;

internal sealed class GetFinancials : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("reports/financials", async (
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] string? groupBy,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            ReportGroupBy grouping = Enum.TryParse(groupBy, ignoreCase: true, out ReportGroupBy parsed)
                ? parsed
                : ReportGroupBy.Month;

            Result<FinancialReportDto> result =
                await sender.Send(new GetFinancialReportQuery(from, to, grouping), cancellationToken);

            return result.Match(
                report => Results.Ok(report),
                CustomResults.Problem);
        })
        .HasPermission("financial:read")
        .WithTags(Tags.Reports)
        .WithName("GetFinancialReport")
        .Produces<FinancialReportDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
