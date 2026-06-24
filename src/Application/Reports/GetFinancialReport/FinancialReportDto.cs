namespace Application.Reports.GetFinancialReport;

public enum ReportGroupBy
{
    Day,
    Week,
    Month
}

/// <summary>
/// Backend-aggregated financial report so the frontend renders charts directly
/// from server data instead of fetching raw transactions and aggregating client-side.
/// Shape mirrors the frontend FinancialReportDto (src/types/dashboard).
/// </summary>
public sealed record FinancialReportDto(
    DateTime From,
    DateTime To,
    string GroupBy,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetResult,
    IReadOnlyList<FinancialReportPeriodDto> ByPeriod);

public sealed record FinancialReportPeriodDto(
    string Period,
    decimal Income,
    decimal Expense,
    decimal Net);
