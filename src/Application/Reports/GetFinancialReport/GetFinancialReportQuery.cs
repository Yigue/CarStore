using Application.Abstractions.Messaging;

namespace Application.Reports.GetFinancialReport;

public sealed record GetFinancialReportQuery(
    DateTime From,
    DateTime To,
    ReportGroupBy GroupBy,
    // sfc B0.3.1: the report covers one currency at a time; ARS when omitted.
    string? Currency = null,
    // sfc B0.3.1 / ADR C2: when set, everything is shown in this currency using each operation's frozen rate.
    string? PresentationCurrency = null) : IQuery<FinancialReportDto>, IReportRangeQuery;
