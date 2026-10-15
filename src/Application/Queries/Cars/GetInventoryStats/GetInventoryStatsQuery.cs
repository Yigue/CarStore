using Application.Abstractions.Messaging;

namespace Application.Queries.Cars.GetInventoryStats;

/// <param name="GroupByBranch">sfc C2.1 / RN-048: also return stock grouped by branch.</param>
public sealed record GetInventoryStatsQuery(bool GroupByBranch = false) : IQuery<InventoryStatsResponse>;
