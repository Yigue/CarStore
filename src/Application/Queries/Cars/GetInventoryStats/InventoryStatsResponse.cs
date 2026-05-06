using SharedKernel;

namespace Application.Queries.Cars.GetInventoryStats;

public sealed record InventoryStatsResponse(
    int TotalCount,
    decimal TotalValue,
    InventoryByStatus ByStatus,
    Dictionary<string, int> ByBrand,
    Dictionary<string, int> ByType);

public sealed record InventoryByStatus(
    int Available,
    int Reserved,
    int Sold);