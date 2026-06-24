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
/// </summary>
public sealed record FinancialReportDto(
    DateTime From,
    DateTime To,
    ReportGroupBy GroupBy,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal NetTotal,
    IReadOnlyList<FinancialReportPeriodDto> Periods);

public sealed record FinancialReportPeriodDto(
    string Label,
    DateTime PeriodStart,
    decimal Income,
    decimal Expense,
    decimal Net);
