using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tenancy.Export;

/// <summary>RN-014 (A7): asks for a ZIP with every record of the caller's dealer. The job builds it asynchronously.</summary>
public sealed record RequestTenantExportCommand : ICommand<Guid>;

internal sealed class RequestTenantExportCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenant,
    IUserContext userContext,
    IDateTimeProvider clock) : ICommandHandler<RequestTenantExportCommand, Guid>
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromHours(24);

    public async Task<Result<Guid>> Handle(RequestTenantExportCommand request, CancellationToken cancellationToken)
    {
        Guid dealerId = tenant.DealerId;
        DateTime now = clock.UtcNow;

        // The lifecycle is read straight from the row, not through the 30 s cached reader: the
        // cache can be stale right after a purge and an export must never resurrect purged data.
        TenantLifecycleStatus? lifecycle = await context.DealerSettings
            .Where(d => d.DealerId == dealerId)
            .Select(d => (TenantLifecycleStatus?)d.LifecycleStatus)
            .FirstOrDefaultAsync(cancellationToken);

        if (lifecycle is null or TenantLifecycleStatus.Purged)
            return Result.Failure<Guid>(TenantExportErrors.NotFound);

        if (lifecycle == TenantLifecycleStatus.Locked)
            return Result.Failure<Guid>(TenantExportErrors.NotAvailable);

        bool inProgress = await context.TenantExports
            .AnyAsync(e => e.DealerId == dealerId
                           && (e.Status == TenantExportStatus.Requested || e.Status == TenantExportStatus.Running),
                cancellationToken);
        if (inProgress)
            return Result.Failure<Guid>(TenantExportErrors.AlreadyInProgress);

        // A failed attempt produced nothing, so it does not consume the daily allowance.
        DateTime windowStart = now - MinimumInterval;
        bool recent = await context.TenantExports
            .AnyAsync(e => e.DealerId == dealerId
                           && e.Status != TenantExportStatus.Failed
                           && e.RequestedAtUtc > windowStart,
                cancellationToken);
        if (recent)
            return Result.Failure<Guid>(TenantExportErrors.RateLimited);

        var export = TenantExport.Request(dealerId, userContext.UserId, now);
        context.TenantExports.Add(export);
        await context.SaveChangesAsync(cancellationToken);

        return export.Id;
    }
}
