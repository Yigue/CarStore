using Application.Abstractions.Messaging;
using Application.Platform.Common;

namespace Application.Platform.Metrics.GetPlatformMetrics;

public sealed record GetPlatformMetricsQuery : IQuery<PlatformMetricsResponse>;
