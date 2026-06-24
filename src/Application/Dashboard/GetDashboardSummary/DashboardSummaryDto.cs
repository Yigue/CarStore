namespace Application.Dashboard.GetDashboardSummary;

/// <summary>
/// Aggregated KPIs for the dashboard landing view. All figures are tenant-scoped
/// via the EF Core global query filters; revenue counts only completed sales.
/// Shape mirrors the frontend DashboardSummaryDto (src/types/dashboard).
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

/// <summary><c>Month</c> is formatted "yyyy-MM" to match the frontend contract.</summary>
public sealed record RevenueByMonthDto(string Month, decimal Amount);
