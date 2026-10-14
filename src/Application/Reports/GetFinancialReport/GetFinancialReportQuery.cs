using Application.Abstractions.Messaging;

namespace Application.Reports.GetFinancialReport;

public sealed record GetFinancialReportQuery(
    DateTime From,
    DateTime To,
    ReportGroupBy GroupBy,
    // sfc B0.3.1: the report covers one currency at a time; ARS when omitted.
    string? Currency = null) : IQuery<FinancialReportDto>, IReportRangeQuery;
