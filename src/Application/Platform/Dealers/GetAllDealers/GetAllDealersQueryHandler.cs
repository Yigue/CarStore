using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Platform.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Platform.Dealers.GetAllDealers;

internal sealed class GetAllDealersQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllDealersQuery, IReadOnlyList<PlatformDealerResponse>>
{
    public async Task<Result<IReadOnlyList<PlatformDealerResponse>>> Handle(
        GetAllDealersQuery query,
        CancellationToken cancellationToken)
    {
        var entities = await context.DealerSettings
            .IgnoreQueryFilters()
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var dealers = entities
            .Select(d => new PlatformDealerResponse(
                d.Id,
                d.DealerId,
                d.DealerName,
                d.ContactEmail,
                d.IsActive,
                d.SuspendedAt,
                d.SuspendReason,
                d.CreatedAt,
                d.CustomDomain,
                "v" + d.RowVersion.ToString()))
            .ToList();

        return Result.Success<IReadOnlyList<PlatformDealerResponse>>(dealers);
    }
}
