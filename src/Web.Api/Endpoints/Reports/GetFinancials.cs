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
            [FromQuery] string? from,
            [FromQuery] string? to,
            [FromQuery] string? groupBy,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            {
                return CustomResults.Problem(Result.Failure(Error.Validation(
                    "MISSING_DATE_RANGE", "Both 'from' and 'to' are required, as valid dates.")));
            }

            if (!DateTime.TryParse(from, out DateTime parsedFrom) || !DateTime.TryParse(to, out DateTime parsedTo))
            {
                return CustomResults.Problem(Result.Failure(Error.Validation(
                    "MISSING_DATE_RANGE", "Both 'from' and 'to' are required, as valid dates.")));
            }

            parsedFrom = parsedFrom.ToUtc();
            parsedTo = parsedTo.ToUtc();

            ReportGroupBy grouping;
            if (string.IsNullOrWhiteSpace(groupBy))
            {
                grouping = ReportGroupBy.Month;
            }
            else if (Enum.TryParse(groupBy, ignoreCase: true, out ReportGroupBy parsed) && Enum.IsDefined(typeof(ReportGroupBy), parsed))
            {
                grouping = parsed;
            }
            else
            {
                return CustomResults.Problem(Result.Failure(Error.Validation(
                    "INVALID_GROUP_BY", "groupBy must be one of: Day, Week, Month, Year.")));
            }

            Result<FinancialReportDto> result =
                await sender.Send(new GetFinancialReportQuery(parsedFrom, parsedTo, grouping), cancellationToken);

            return result.Match(
                report => Results.Ok(report),
                CustomResults.Problem);
        })
        .HasPermission("financial:read")
        .WithTags(Tags.Reports)
        .WithName("GetFinancialReport")
        .Produces<FinancialReportDto>(StatusCodes.Status200OK);
    }
}
