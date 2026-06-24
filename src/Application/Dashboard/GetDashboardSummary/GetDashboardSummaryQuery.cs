using Application.Abstractions.Messaging;

namespace Application.Dashboard.GetDashboardSummary;

public sealed record GetDashboardSummaryQuery : IQuery<DashboardSummaryDto>;
