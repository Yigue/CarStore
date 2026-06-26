using System.Globalization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
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
internal sealed class GetFinancialReportQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService)
    : IQueryHandler<GetFinancialReportQuery, FinancialReportDto>
{
    public async Task<Result<FinancialReportDto>> Handle(
        GetFinancialReportQuery query,
        CancellationToken cancellationToken)
    {
        // REQ-FIN-TENANT-001 (audit-gap #13): defense-in-handler. The EF GQF
        // already filters by DealerId, but the reports endpoint runs through
        // a raw SQL-shaped path that may bypass it. Explicitly require a
        // tenant context here so we never aggregate across tenants.
        var guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return Result.Failure<FinancialReportDto>(guard.Error);
        }

        DateTime from = query.From;
        DateTime to = query.To;

        var rows = await context.Transactions.AsNoTracking()
            .Where(t => t.DealerId == tenantService.DealerId)
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to)
            .Select(t => new TransactionRow(t.TransactionDate, t.Type, t.Amount.Amount))
            .ToListAsync(cancellationToken);

        // Generate all period boundary start dates within [from, to] inclusive
        var allPeriods = new List<DateTime>();
        DateTime current = PeriodStart(from, query.GroupBy);
        DateTime last = PeriodStart(to, query.GroupBy);

        while (current <= last)
        {
            allPeriods.Add(current);
            current = query.GroupBy switch
            {
                ReportGroupBy.Day => current.AddDays(1),
                ReportGroupBy.Week => current.AddDays(7),
                ReportGroupBy.Month => current.AddMonths(1),
                ReportGroupBy.Year => current.AddYears(1),
                _ => current.AddMonths(1)
            };
        }

        var rowsByPeriod = rows
            .GroupBy(r => PeriodStart(r.Date, query.GroupBy))
            .ToDictionary(g => g.Key, g => g.ToList());

        var series = allPeriods
            .Select(periodStart =>
            {
                decimal income = 0;
                decimal expense = 0;
                if (rowsByPeriod.TryGetValue(periodStart, out var periodRows))
                {
                    income = periodRows.Where(x => x.Type == TransactionType.Income).Sum(x => x.Amount);
                    expense = periodRows.Where(x => x.Type == TransactionType.Expense).Sum(x => x.Amount);
                }

                return new FinancialReportSeriesDto(
                    Label(periodStart, query.GroupBy),
                    income,
                    expense,
                    income - expense);
            })
            .ToList();

        decimal totalIncome = series.Sum(p => p.Income);
        decimal totalExpense = series.Sum(p => p.Expense);

        var dto = new FinancialReportDto(
            "ARS",
            from,
            to,
            query.GroupBy.ToString(),
            series,
            new FinancialReportTotalsDto(totalIncome, totalExpense, totalIncome - totalExpense));

        return Result.Success(dto);
    }

    private static DateTime PeriodStart(DateTime date, ReportGroupBy groupBy) => groupBy switch
    {
        ReportGroupBy.Day => date.Date,
        ReportGroupBy.Week => StartOfWeek(date.Date),
        ReportGroupBy.Month => new DateTime(date.Year, date.Month, 1, 0, 0, 0, date.Kind),
        ReportGroupBy.Year => new DateTime(date.Year, 1, 1, 0, 0, 0, date.Kind),
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
        ReportGroupBy.Year => periodStart.ToString("yyyy", CultureInfo.InvariantCulture),
        _ => periodStart.ToString("yyyy-MM", CultureInfo.InvariantCulture)
    };

    private static int ISOYear(DateTime date) => ISOWeek.GetYear(date);

    private readonly record struct TransactionRow(DateTime Date, TransactionType Type, decimal Amount);
}
