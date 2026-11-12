using SharedKernel;

namespace Application.Queries.Cars.GetInventoryStats;

public sealed record InventoryStatsResponse(
    int TotalCount,
    decimal TotalValue,
    InventoryByStatus ByStatus,
    Dictionary<string, int> ByBrand,
    Dictionary<string, int> ByType,
    // sfc C2.1 / RN-048: only when grouping by branch was asked for.
    IReadOnlyList<BranchInventory>? ByBranch = null);

/// <summary>sfc C2.1: vehicles and own-inventory value (consigned cars excluded) of one branch.</summary>
public sealed record BranchInventory(Guid BranchId, string Name, int Count, decimal Value);

public sealed record InventoryByStatus(
    int Available,
    int Reserved,
    int Sold);