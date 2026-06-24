using Application.Abstractions.Messaging;

namespace Application.Reports.GetFinancialReport;

public sealed record GetFinancialReportQuery(
    DateTime From,
    DateTime To,
    ReportGroupBy GroupBy) : IQuery<FinancialReportDto>;
