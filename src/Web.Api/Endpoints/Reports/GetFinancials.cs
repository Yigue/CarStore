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
            // REQ-FIN-REPORT-001: FinancialTransaction.TransactionDate is mapped by EF Core
            // convention to PostgreSQL `timestamp without time zone` (no explicit Configuration
            // file exists for this aggregate). ASP.NET model-binds ISO strings with a trailing
            // `Z` as DateTimeKind.Utc. Npgsql 6+ refuses to compare a DateTime(Utc) against
            // a `timestamp without time zone` column → InvalidCastException → 500.
            // Strip the Kind so EF binds cleanly to the convention-mapped column.
            from = DateTime.SpecifyKind(from, DateTimeKind.Unspecified);
            to = DateTime.SpecifyKind(to, DateTimeKind.Unspecified);

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
