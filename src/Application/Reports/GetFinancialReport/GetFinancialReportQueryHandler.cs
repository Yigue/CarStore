using System.Globalization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.GetFinancialReport;

/// <summary>
/// Aggregates financial transactions into income/expense/net buckets per period.
/// Transactions in the [From, To] window are pulled with a minimal projection and
/// bucketed in memory: Day/Month group cleanly in SQL, but Week (ISO-week, Monday
/// start) does not translate reliably across providers, so all three modes share
/// one in-memory grouping path for consistency. The date window keeps the set small.
/// </summary>
internal sealed class GetFinancialReportQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetFinancialReportQuery, FinancialReportDto>
{
    public async Task<Result<FinancialReportDto>> Handle(
        GetFinancialReportQuery query,
        CancellationToken cancellationToken)
    {
        DateTime from = query.From;
        DateTime toExclusive = query.To;

        var rows = await context.Transactions.AsNoTracking()
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= toExclusive)
            .Select(t => new TransactionRow(t.TransactionDate, t.Type, t.Amount.Amount))
            .ToListAsync(cancellationToken);

        var periods = rows
            .GroupBy(r => PeriodStart(r.Date, query.GroupBy))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                decimal income = g.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
                decimal expense = g.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
                return new FinancialReportPeriodDto(
                    Label(g.Key, query.GroupBy),
                    g.Key,
                    income,
                    expense,
                    income - expense);
            })
            .ToList();

        decimal totalIncome = periods.Sum(p => p.Income);
        decimal totalExpense = periods.Sum(p => p.Expense);

        var dto = new FinancialReportDto(
            from,
            toExclusive,
            query.GroupBy,
            totalIncome,
            totalExpense,
            totalIncome - totalExpense,
            periods);

        return Result.Success(dto);
    }

    private static DateTime PeriodStart(DateTime date, ReportGroupBy groupBy) => groupBy switch
    {
        ReportGroupBy.Day => date.Date,
        ReportGroupBy.Week => StartOfWeek(date.Date),
        ReportGroupBy.Month => new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind),
        _ => new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind)
    };

    // ISO-8601 week: Monday is the first day.
    private static DateTime StartOfWeek(DateTime date)
    {
        int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        return date.AddDays(-diff).Date;
    }

    private static string Label(DateTime periodStart, ReportGroupBy groupBy) => groupBy switch
    {
        ReportGroupBy.Day => periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ReportGroupBy.Week => $"{ISOYear(periodStart)}-W{ISOWeek.GetWeekOfYear(periodStart):D2}",
        ReportGroupBy.Month => periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        _ => periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture)
    };

    private static int ISOYear(DateTime date) => ISOWeek.GetYear(date);

    private readonly record struct TransactionRow(DateTime Date, TransactionType Type, decimal Amount);
}
