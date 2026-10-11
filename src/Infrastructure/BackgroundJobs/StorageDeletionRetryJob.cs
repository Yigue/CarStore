using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Follow-up to A1.4: retries the storage deletes that failed right after a tenant purge committed (they are parked in
/// <c>PendingStorageDeletions</c>). Every 15 minutes it takes a bounded batch, oldest first. A key that deletes (or is
/// already gone: <see cref="IStorageService.DeleteFileAsync"/> is idempotent and does not throw for a missing object)
/// has its row removed; a failure bumps Attempts and keeps the row. After <see cref="PendingStorageDeletion.MaxAttempts"/>
/// the key is no longer retried but the row is NEVER deleted: it is logged at Error on every run for a human to review.
/// It does not depend on <c>Retention:PurgeEnabled</c>: it only touches objects of tenants that were already purged.
/// Single-instance assumption: Quartz uses the in-memory RAMJobStore (not clustered) and
/// <see cref="DisallowConcurrentExecutionAttribute"/> only guards one process; with several API replicas two runs could
/// race on the same row, which is harmless here (the delete is idempotent; the loser's row update is a no-op).
/// </summary>
[DisallowConcurrentExecution]
public sealed class StorageDeletionRetryJob(
    IApplicationDbContext context,
    IStorageService storage,
    ILogger<StorageDeletionRetryJob> logger) : IJob
{
    public const int BatchSize = 100;

    public async Task Execute(IJobExecutionContext jobContext)
    {
        CancellationToken ct = jobContext.CancellationToken;

        // Not tenant-filtered by configuration; IgnoreQueryFilters is kept for safety against a future filter.
        List<PendingStorageDeletion> batch = await context.PendingStorageDeletions
            .IgnoreQueryFilters()
            .Where(p => p.Attempts < PendingStorageDeletion.MaxAttempts)
            .OrderBy(p => p.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (PendingStorageDeletion pending in batch)
        {
            try
            {
                await storage.DeleteFileAsync(pending.ObjectKey, ct);
                context.PendingStorageDeletions.Remove(pending);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                pending.RecordFailure(ex.Message);
                logger.LogWarning(ex,
                    "Retry of storage deletion {ObjectKey} (dealer {DealerId}) failed, attempt {Attempts}",
                    pending.ObjectKey, pending.DealerId, pending.Attempts);
            }

            // Persist per row so a later failure (or a crash) cannot lose the outcome of earlier rows.
            try
            {
                await context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not persist the retry outcome of storage deletion {ObjectKey} (dealer {DealerId})",
                    pending.ObjectKey, pending.DealerId);
            }
        }

        List<PendingStorageDeletion> exhausted = await context.PendingStorageDeletions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.Attempts >= PendingStorageDeletion.MaxAttempts)
            .OrderBy(p => p.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (PendingStorageDeletion stuck in exhausted)
        {
            logger.LogError(
                "Storage object {ObjectKey} of dealer {DealerId} could not be deleted after {Attempts} attempts; the row stays in PendingStorageDeletions for manual review. Last error: {LastError}",
                stuck.ObjectKey, stuck.DealerId, stuck.Attempts, stuck.LastError);
        }
    }
}
