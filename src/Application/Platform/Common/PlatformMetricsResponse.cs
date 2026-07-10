namespace Application.Platform.Common;

public sealed record PlatformMetricsResponse(
    int TotalDealers,
    int ActiveDealers,
    int SuspendedDealers,
    int TotalUsers,
    MrrStub Mrr);
