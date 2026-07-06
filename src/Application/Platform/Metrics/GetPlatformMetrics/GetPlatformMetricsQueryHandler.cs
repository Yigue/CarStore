using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Platform.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Platform.Metrics.GetPlatformMetrics;

internal sealed class GetPlatformMetricsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetPlatformMetricsQuery, PlatformMetricsResponse>
{
    public async Task<Result<PlatformMetricsResponse>> Handle(
        GetPlatformMetricsQuery query,
        CancellationToken cancellationToken)
    {
        var dealers = await context.DealerSettings
            .IgnoreQueryFilters()
            .Select(d => d.IsActive)
            .ToListAsync(cancellationToken);

        var total = dealers.Count;
        var active = dealers.Count(isActive => isActive);
        var suspended = total - active;

        var totalUsers = await context.Users.IgnoreQueryFilters().CountAsync(cancellationToken);

        return Result.Success(new PlatformMetricsResponse(
            total,
            active,
            suspended,
            totalUsers,
            new MrrStub(0m, "USD", true, "Requires saas-subscription-payments")));
    }
}
