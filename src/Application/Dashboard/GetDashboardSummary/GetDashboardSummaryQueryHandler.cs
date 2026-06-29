using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars.Attributes;
using Domain.Leads;
using Domain.Quotes.Attributes;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Dashboard.GetDashboardSummary;

/// <summary>
/// Builds the dashboard KPIs in a single round of server-side aggregations,
/// replacing the previous N+1 of one HTTP call per metric.
///
/// NOTE: queries run sequentially, not via Task.WhenAll — a single EF Core
/// DbContext is not thread-safe and concurrent queries throw
/// InvalidOperationException ("A second operation was started on this context").
/// Each query is a cheap aggregate, so sequential latency stays well under budget.
/// </summary>
internal sealed class GetDashboardSummaryQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<Result<DashboardSummaryDto>> Handle(
        GetDashboardSummaryQuery query,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime windowStart = monthStart.AddMonths(-11);

        IQueryable<Domain.Sales.Sale> completedSales =
            context.Sales.AsNoTracking().Where(s => s.Status == SaleStatus.Completed);

        decimal totalRevenue = await completedSales
            .SumAsync(s => (decimal?)EF.Property<decimal>(s, "FinalPrice"), cancellationToken) ?? 0m;

        decimal revenueThisMonth = await completedSales
            .Where(s => s.SaleDate >= monthStart)
            .SumAsync(s => (decimal?)EF.Property<decimal>(s, "FinalPrice"), cancellationToken) ?? 0m;

        int totalSales = await completedSales.CountAsync(cancellationToken);

        int salesThisMonth = await completedSales
            .Where(s => s.SaleDate >= monthStart)
            .CountAsync(cancellationToken);

        int totalLeads = await context.Leads.AsNoTracking().CountAsync(cancellationToken);

        int wonLeads = await context.Leads.AsNoTracking()
            .CountAsync(l => l.Status == LeadStatus.Ganado, cancellationToken);

        int activeLeads = await context.Leads.AsNoTracking()
            .CountAsync(
                l => l.Status != LeadStatus.Ganado
                  && l.Status != LeadStatus.Perdido
                  && l.Status != LeadStatus.Archivado,
                cancellationToken);

        decimal conversionRate = totalLeads == 0
            ? 0m
            : Math.Round((decimal)wonLeads / totalLeads * 100m, 2);

        int activeInventory = await context.Cars.AsNoTracking()
            .CountAsync(
                c => c.ServiceCar == StatusServiceCar.Disponible
                  || c.ServiceCar == StatusServiceCar.EnVenta,
                cancellationToken);

        int pendingQuotes = await context.Quotes.AsNoTracking()
            .CountAsync(q => q.Status == QuoteStatus.Pending, cancellationToken);

        int upcomingAppointments = await context.Appointments.AsNoTracking()
            .CountAsync(a => a.StartDateTime >= now, cancellationToken);

        // Last 12 months revenue, grouped in SQL then gap-filled in memory.
        var grouped = await completedSales
            .Where(s => s.SaleDate >= windowStart)
            .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Revenue = g.Sum(s => EF.Property<decimal>(s, "FinalPrice"))
            })
            .ToListAsync(cancellationToken);

        var revenueByMonth = new List<RevenueByMonthDto>(12);
        for (int i = 0; i < 12; i++)
        {
            DateTime month = windowStart.AddMonths(i);
            decimal revenue = grouped
                .FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Revenue ?? 0m;
            revenueByMonth.Add(new RevenueByMonthDto($"{month.Year:D4}-{month.Month:D2}", revenue));
        }

        var dto = new DashboardSummaryDto(
            totalRevenue,
            revenueThisMonth,
            totalSales,
            salesThisMonth,
            activeLeads,
            conversionRate,
            activeInventory,
            pendingQuotes,
            upcomingAppointments,
            revenueByMonth);

        return Result.Success(dto);
    }
}
