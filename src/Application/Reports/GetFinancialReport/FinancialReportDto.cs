namespace Application.Reports.GetFinancialReport;

public enum ReportGroupBy
{
    Day,
    Week,
    Month,
    Year
}

/// <summary>
/// Backend-aggregated financial report so the frontend renders charts directly
/// from server data instead of fetching raw transactions and aggregating client-side.
/// Shape mirrors the frontend FinancialReportDto (src/types/dashboard).
/// </summary>
public sealed record FinancialReportDto(
    string Currency,
    DateTime From,
    DateTime To,
    string GroupBy,
    IReadOnlyList<FinancialReportSeriesDto> Series,
    FinancialReportTotalsDto Totals);

public sealed record FinancialReportSeriesDto(
    string Bucket,
    decimal Income,
    decimal Expense,
    decimal Balance);

public sealed record FinancialReportTotalsDto(
    decimal Income,
    decimal Expense,
    decimal Balance);
