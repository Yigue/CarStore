using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Tenancy.Purge;

/// <summary>
/// RN-013 (A1.4): the daily platform run. Step 1 moves tenants whose grace period ended to
/// <c>Locked</c>; step 2 sends ONE <see cref="PurgeTenantCommand"/> per Locked tenant whose purge date was reached.
/// </summary>
public sealed record PurgeDueTenantsCommand(bool DryRun) : ICommand<PurgeRunSummary>;

public sealed record PurgeRunSummary(bool DryRun, int Locked, int Purged, int Skipped, int Failed);

internal sealed class PurgeDueTenantsCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    IDateTimeProvider clock,
    ITenantLifecycleCache lifecycleCache,
    IConfiguration configuration,
    ILogger<PurgeDueTenantsCommandHandler> logger) : ICommandHandler<PurgeDueTenantsCommand, PurgeRunSummary>
{
    public const string GraceDaysKey = "Retention:GraceDays";
    public const int DefaultGraceDays = 30;

    public async Task<Result<PurgeRunSummary>> Handle(PurgeDueTenantsCommand request, CancellationToken ct)
    {
        DateTime now = clock.UtcNow;
        int graceDays = int.TryParse(configuration[GraceDaysKey], out int configured) && configured > 0
            ? configured
            : DefaultGraceDays;

        int locked = await LockExpiredGraceAsync(now, graceDays, ct);

        // Platform job: no tenant in scope.
        List<Guid> due = await context.DealerSettings
            .IgnoreQueryFilters()
            .Where(d => d.LifecycleStatus == TenantLifecycleStatus.Locked
                        && d.PurgeScheduledAt != null
                        && d.PurgeScheduledAt <= now)
            .Select(d => d.DealerId)
            .ToListAsync(ct);

        int purged = 0, skipped = 0, failed = 0;
        foreach (Guid dealerId in due)
        {
            try
            {
                Result<TenantPurgeResult> result = await sender.Send(new PurgeTenantCommand(dealerId, request.DryRun), ct);
                if (result.IsSuccess)
                    purged++;
                else
                    skipped++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                logger.LogError(ex, "Purge of tenant {DealerId} failed; it stays Locked and is retried on the next run", dealerId);
            }
        }

        logger.LogInformation(
            "Tenant purge run (dryRun={DryRun}): locked={Locked} purged={Purged} skipped={Skipped} failed={Failed}",
            request.DryRun, locked, purged, skipped, failed);

        return new PurgeRunSummary(request.DryRun, locked, purged, skipped, failed);
    }

    private async Task<int> LockExpiredGraceAsync(DateTime now, int graceDays, CancellationToken ct)
    {
        List<Domain.DealerSettings.DealerSettings> inGrace = await context.DealerSettings
            .IgnoreQueryFilters()
            .Where(d => d.LifecycleStatus == TenantLifecycleStatus.CancelledGrace && d.CancelledAt != null)
            .ToListAsync(ct);

        var lockedIds = new List<Guid>();
        foreach (Domain.DealerSettings.DealerSettings dealer in inGrace)
        {
            if (dealer.LockAfterGrace(now, graceDays))
                lockedIds.Add(dealer.DealerId);
        }

        if (lockedIds.Count == 0)
            return 0;

        await context.SaveChangesAsync(ct);
        foreach (Guid id in lockedIds)
            lifecycleCache.Invalidate(id);

        return lockedIds.Count;
    }
}
