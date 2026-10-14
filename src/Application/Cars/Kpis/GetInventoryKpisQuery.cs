using Application.Abstractions.Messaging;

namespace Application.Cars.Kpis;

/// <summary>
/// sfc B6.1 / RN-042: per-vehicle inventory KPIs. A snapshot of the current stock and its sales, not a period report,
/// so it lives outside <c>Application.Reports</c> (whose queries all take a date range).
/// </summary>
public sealed record GetInventoryKpisQuery : IQuery<IReadOnlyList<InventoryKpiResponse>>;

/// <param name="DaysInStock">Calendar days from entry (Ingreso) to the completed sale, or to today while in stock.</param>
/// <param name="Currency">The sale currency once sold, otherwise the price currency. Every amount below is in it.</param>
/// <param name="Margin">Sale price − purchase cost − completed reconditioning; null until sold, or when a cost cannot
/// be expressed in the sale currency.</param>
public sealed record InventoryKpiResponse(
    Guid CarId,
    string Patente,
    string Status,
    DateTime EnteredAtUtc,
    DateTime? SoldAtUtc,
    int DaysInStock,
    string Currency,
    decimal? SalePrice,
    decimal? PurchaseCost,
    decimal? ReconditioningCost,
    decimal? Margin);
