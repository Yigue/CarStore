using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Platform.Common;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Platform.Dealers.SuspendDealer;

internal sealed class SuspendDealerCommandHandler(IApplicationDbContext context)
    : ICommandHandler<SuspendDealerCommand, PlatformDealerResponse>
{
    public async Task<Result<PlatformDealerResponse>> Handle(
        SuspendDealerCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            return Result.Failure<PlatformDealerResponse>(DealerSettingsErrors.SuspendReasonRequired);

        var dealer = await context.DealerSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == command.DealerId, cancellationToken);

        if (dealer is null)
            return Result.Failure<PlatformDealerResponse>(PlatformErrors.DealerNotFound);

        var currentETag = "v" + dealer.RowVersion.ToString();
        if (currentETag != command.ETag)
            return Result.Failure<PlatformDealerResponse>(PlatformErrors.ETagMismatch);

        dealer.Suspend(command.Reason, command.ActorId, DateTime.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapToResponse(dealer));
    }

    private static PlatformDealerResponse MapToResponse(Domain.DealerSettings.DealerSettings dealer) =>
        new(
            dealer.Id,
            dealer.DealerId,
            dealer.DealerName,
            dealer.ContactEmail,
            dealer.IsActive,
            dealer.SuspendedAt,
            dealer.SuspendReason,
            dealer.CreatedAt,
            dealer.CustomDomain,
            "v" + dealer.RowVersion.ToString());
}
