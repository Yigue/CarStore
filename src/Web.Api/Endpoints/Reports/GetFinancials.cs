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
                return Results.BadRequest(new { error = "MISSING_DATE_RANGE" });
            }

            if (!DateTime.TryParse(from, out DateTime parsedFrom) || !DateTime.TryParse(to, out DateTime parsedTo))
            {
                return Results.BadRequest(new { error = "MISSING_DATE_RANGE" });
            }

            // REQ-FIN-REPORT-001: FinancialTransaction.TransactionDate is mapped by EF Core
            // convention to PostgreSQL `timestamp without time zone` (no explicit Configuration
            // file exists for this aggregate). ASP.NET model-binds ISO strings with a trailing
            // `Z` as DateTimeKind.Utc. Npgsql 6+ refuses to compare a DateTime(Utc) against
            // a `timestamp without time zone` column → InvalidCastException → 500.
            // Strip the Kind so EF binds cleanly to the convention-mapped column.
            parsedFrom = DateTime.SpecifyKind(parsedFrom, DateTimeKind.Unspecified);
            parsedTo = DateTime.SpecifyKind(parsedTo, DateTimeKind.Unspecified);

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
                return Results.BadRequest(new 
                { 
                    error = "INVALID_GROUP_BY",
                    allowedValues = new[] { "Day", "Week", "Month", "Year" }
                });
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
        .Produces<FinancialReportDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
