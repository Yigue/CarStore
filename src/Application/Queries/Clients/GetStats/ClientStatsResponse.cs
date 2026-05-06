using SharedKernel;

namespace Application.Queries.Clients.GetStats;

public sealed record ClientStatsResponse(
    int TotalCount,
    Dictionary<string, int> BySource,
    int RecentCount,
    int ActiveCount);