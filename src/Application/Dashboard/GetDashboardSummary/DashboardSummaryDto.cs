namespace Application.Dashboard.GetDashboardSummary;

/// <summary>
/// Aggregated KPIs for the dashboard landing view. All figures are tenant-scoped
/// via the EF Core global query filters; revenue counts only completed sales.
/// </summary>
public sealed record DashboardSummaryDto(
    decimal TotalRevenue,
    decimal RevenueThisMonth,
    int TotalSales,
    int SalesThisMonth,
    int ActiveLeads,
    decimal ConversionRate,
    int ActiveInventory,
    int PendingQuotes,
    int UpcomingAppointments,
    IReadOnlyList<RevenueByMonthDto> RevenueByMonth);

public sealed record RevenueByMonthDto(int Year, int Month, decimal Revenue);
