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
    FinancialReportTotalsDto Totals,
    // sfc B0.3.1: every currency with movements in the window, so the client can offer the other report(s).
    IReadOnlyList<string>? AvailableCurrencies = null,
    // sfc B0.3.1 / ADR C2: only with a presentation currency. Transactions with no usable frozen rate are left out of
    // the totals and listed here, never converted with today's rate.
    FinancialReportUnconvertedDto? Unconverted = null);

public sealed record FinancialReportUnconvertedDto(int Count, IReadOnlyList<FinancialReportUnconvertedDto.Entry> ByCurrency)
{
    public sealed record Entry(string Currency, decimal Income, decimal Expense);
}

public sealed record FinancialReportSeriesDto(
    string Bucket,
    decimal Income,
    decimal Expense,
    decimal Balance);

public sealed record FinancialReportTotalsDto(
    decimal Income,
    decimal Expense,
    decimal Balance);
