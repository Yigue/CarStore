using SharedKernel;

namespace Application.Queries.Sales.GetSummary;

public sealed record SalesSummaryResponse(
    decimal TotalAmount,
    int TotalCount,
    decimal AveragePrice,
    SalesByStatus ByStatus);

public sealed record SalesByStatus(
    int Pending,
    int Completed,
    int Cancelled);