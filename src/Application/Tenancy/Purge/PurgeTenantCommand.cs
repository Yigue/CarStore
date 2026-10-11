using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Tenancy.Purge;

/// <summary>
/// RN-013 (A1.4): removes the personal and operational data of ONE tenant. It only runs for a tenant that is
/// <see cref="TenantLifecycleStatus.Locked"/> with a purge date already reached; for anything else it is a no-op
/// that answers <see cref="TenantPurgeErrors.NotEligible"/>. <paramref name="DryRun"/> only counts.
/// </summary>
public sealed record PurgeTenantCommand(Guid DealerId, bool DryRun) : ICommand<TenantPurgeResult>;

/// <summary>Rows removed (or, in a dry run, that would be removed) per aggregate.</summary>
public sealed record TenantPurgeResult(bool DryRun, IReadOnlyDictionary<string, int> Counts);

public static class TenantPurgeErrors
{
    public static readonly Error NotFound = new(
        "TenantPurge.NotFound", "No se encontró el tenant a purgar.", ErrorType.NotFound);

    public static readonly Error NotEligible = new(
        "TenantPurge.NotEligible",
        "Solo se puede purgar un tenant bloqueado cuya fecha de purga ya pasó.", ErrorType.Conflict);

    public static readonly Error ExportStillAvailable = new(
        "TenantPurge.ExportStillAvailable",
        "El tenant todavía tiene una exportación en curso o descargable; la purga se posterga hasta que venza.",
        ErrorType.Conflict);
}

internal sealed class PurgeTenantCommandHandler(
    IApplicationDbContext context,
    IStorageService storage,
    IDateTimeProvider clock,
    ITenantLifecycleCache lifecycleCache,
    ILogger<PurgeTenantCommandHandler> logger) : ICommandHandler<PurgeTenantCommand, TenantPurgeResult>
{
    public const int BatchSize = 200;
    // Provider name ends with "PostgreSQL"; matched by suffix because Application must not reference the driver name.
    private const string PostgresProviderSuffix = "EntityFrameworkCore.PostgreSQL";

    public async Task<Result<TenantPurgeResult>> Handle(PurgeTenantCommand request, CancellationToken ct)
    {
        Guid dealerId = request.DealerId;
        if (dealerId == Guid.Empty)
            return Result.Failure<TenantPurgeResult>(TenantPurgeErrors.NotFound);

        DateTime now = clock.UtcNow;

        // Fast path: a read-only pre-check (also the whole dry run). It is NOT trusted for the delete: the state can
        // change right after it, so the write path below re-validates inside a transaction holding a row lock.
        Result<DealerSettingsEntity> precheck = await CheckEligibilityAsync(dealerId, now, track: false, ct);
        if (precheck.IsFailure)
            return Result.Failure<TenantPurgeResult>(precheck.Error);

        var counts = new Dictionary<string, int>();

        if (request.DryRun)
        {
            foreach ((string name, IQueryable<object> query) in Scopes(dealerId))
                counts[name] = await query.CountAsync(ct);

            return new TenantPurgeResult(true, counts);
        }

        // One transaction for the re-check and every delete. Relational providers only (InMemory has no transactions).
        DbContext? db = context as DbContext;
        await using IDbContextTransaction? transaction = db is not null && db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        // Row lock on dealer_settings (PostgreSQL only; SQLite serializes writers on its own). Any concurrent writer of
        // this row (e.g. StartTenantRetentionHandler.ClearRetention) blocks on its UPDATE until we commit, and by then
        // the row is Purged; its xmin concurrency token then fails the stale write instead of resurrecting the tenant.
        if (db is not null && db.Database.ProviderName?.EndsWith(PostgresProviderSuffix, StringComparison.Ordinal) == true)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM dealer_settings WHERE dealer_id = {dealerId} FOR UPDATE", ct);
        }

        // Authoritative re-check, tracked, under the lock. A copy of the row already tracked by this context (loaded
        // earlier in the same scope) would be returned stale by the query, so it is detached to force a fresh read.
        foreach (DealerSettingsEntity stale in context.DealerSettings.Local.Where(d => d.DealerId == dealerId).ToList())
            context.DetachEntity(stale);

        Result<DealerSettingsEntity> locked = await CheckEligibilityAsync(dealerId, now, track: true, ct);
        if (locked.IsFailure)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(ct);
            return Result.Failure<TenantPurgeResult>(locked.Error);
        }

        DealerSettingsEntity dealer = locked.Value;

        // 1. Break the Client -> Lead link so both sides can be deleted (clients.origin_lead_id vs leads.converted_client_id).
        await ClearClientOriginLeadsAsync(dealerId, ct);

        // 2. Delete in FK order, children first. Batches are separate SaveChanges but one transaction: a failure rolls
        //    everything back, the tenant stays Locked and the next run starts over (storage deletes are idempotent).
        counts["webhookDeliveries"] = await DeleteAsync(context.WebhookDeliveries.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.WebhookDeliveries, null, ct);
        counts["webhookSubscriptions"] = await DeleteAsync(context.WebhookSubscriptions.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.WebhookSubscriptions, null, ct);
        counts["leadActivities"] = await DeleteAsync(context.LeadActivities.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.LeadActivities, null, ct);
        counts["documents"] = await DeleteAsync(context.Documents.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Documents, d => d.BlobName, ct);
        counts["transactions"] = await DeleteAsync(context.Transactions.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Transactions, null, ct);
        counts["appointments"] = await DeleteAsync(context.Appointments.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Appointments, null, ct);
        counts["sales"] = await DeleteAsync(context.Sales.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Sales, null, ct);
        counts["quotes"] = await DeleteAsync(context.Quotes.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Quotes, null, ct);
        counts["reconditioningTasks"] = await DeleteAsync(context.ReconditioningTasks.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.ReconditioningTasks, null, ct);
        counts["leads"] = await DeleteAsync(context.Leads.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Leads, null, ct);
        counts["carImages"] = await DeleteAsync(CarImagesOf(dealerId), context.CarImages, i => i.ObjectKey, ct);
        counts["cars"] = await DeleteAsync(context.Cars.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Cars, null, ct);
        counts["clients"] = await DeleteAsync(context.Clients.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Clients, null, ct);
        counts["transactionCategories"] = await DeleteAsync(context.TransactionCategories.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.TransactionCategories, null, ct);
        counts["userPermissions"] = await DeleteAsync(context.UserPermissions.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.UserPermissions, null, ct);
        counts["passwordResetTokens"] = await DeleteAsync(PasswordResetTokensOf(dealerId), context.PasswordResetTokens, null, ct);
        counts["users"] = await DeleteAsync(context.Users.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Users, null, ct);
        counts["rolePermissions"] = await DeleteAsync(RolePermissionsOf(dealerId), context.RolePermissions, null, ct);
        counts["roles"] = await DeleteAsync(context.Roles.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.Roles, null, ct);
        counts["tenantExports"] = await DeleteAsync(context.TenantExports.IgnoreQueryFilters().Where(x => x.DealerId == dealerId), context.TenantExports, e => e.ObjectKey, ct);

        // 3. Terminal transition + tombstone, saved together. DealerSubscription, audit logs, legal acceptances,
        //    Stripe events and refund records are NOT touched (billing / evidence retention).
        dealer.Purge(now);
        context.TenantPurgeRecords.Add(TenantPurgeRecord.Create(dealerId, dealer.DealerName, now, counts));
        await context.SaveChangesAsync(ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);
        lifecycleCache.Invalidate(dealerId);

        logger.LogInformation("Tenant {DealerId} purged: {Counts}", dealerId, counts);
        return new TenantPurgeResult(false, counts);
    }

    private async Task<Result<DealerSettingsEntity>> CheckEligibilityAsync(
        Guid dealerId, DateTime now, bool track, CancellationToken ct)
    {
        // Platform job: no tenant in scope, so the filters are bypassed and EVERY query pins DealerId.
        IQueryable<DealerSettingsEntity> query = context.DealerSettings.IgnoreQueryFilters();
        if (!track)
            query = query.AsNoTracking();

        DealerSettingsEntity? dealer = await query.FirstOrDefaultAsync(d => d.DealerId == dealerId, ct);
        if (dealer is null)
            return Result.Failure<DealerSettingsEntity>(TenantPurgeErrors.NotFound);

        if (!dealer.IsDueForPurge(now))
        {
            logger.LogInformation(
                "Purge of tenant {DealerId} skipped: lifecycle {Status}, purge scheduled at {ScheduledAt}",
                dealerId, dealer.LifecycleStatus, dealer.PurgeScheduledAt);
            return Result.Failure<DealerSettingsEntity>(TenantPurgeErrors.NotEligible);
        }

        // RN-014: the dealer can still download a Ready export for up to 7 days after it was built, also while Locked.
        bool exportPending = await context.TenantExports
            .IgnoreQueryFilters()
            .AnyAsync(e => e.DealerId == dealerId
                           && (e.Status == TenantExportStatus.Requested
                               || e.Status == TenantExportStatus.Running
                               || (e.Status == TenantExportStatus.Ready && (e.ExpiresAt == null || e.ExpiresAt > now))),
                ct);
        if (exportPending)
        {
            logger.LogInformation("Purge of tenant {DealerId} deferred: an export is still available", dealerId);
            return Result.Failure<DealerSettingsEntity>(TenantPurgeErrors.ExportStillAvailable);
        }

        return dealer;
    }

    private IEnumerable<(string Name, IQueryable<object> Query)> Scopes(Guid dealerId)
    {
        yield return ("webhookDeliveries", context.WebhookDeliveries.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("webhookSubscriptions", context.WebhookSubscriptions.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("leadActivities", context.LeadActivities.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("documents", context.Documents.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("transactions", context.Transactions.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("appointments", context.Appointments.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("sales", context.Sales.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("quotes", context.Quotes.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("reconditioningTasks", context.ReconditioningTasks.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("leads", context.Leads.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("carImages", CarImagesOf(dealerId));
        yield return ("cars", context.Cars.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("clients", context.Clients.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("transactionCategories", context.TransactionCategories.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("userPermissions", context.UserPermissions.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("passwordResetTokens", PasswordResetTokensOf(dealerId));
        yield return ("users", context.Users.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("rolePermissions", RolePermissionsOf(dealerId));
        yield return ("roles", context.Roles.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
        yield return ("tenantExports", context.TenantExports.IgnoreQueryFilters().Where(x => x.DealerId == dealerId));
    }

    // CarImage, PasswordResetToken and RolePermission carry no DealerId: they are reached through their owner.
    private IQueryable<Domain.Cars.CarImage> CarImagesOf(Guid dealerId) =>
        context.CarImages.Where(i => context.Cars.IgnoreQueryFilters().Any(c => c.Id == i.CarId && c.DealerId == dealerId));

    private IQueryable<Domain.Users.PasswordResetToken> PasswordResetTokensOf(Guid dealerId) =>
        context.PasswordResetTokens.Where(t => context.Users.IgnoreQueryFilters().Any(u => u.Id == t.UserId && u.DealerId == dealerId));

    private IQueryable<Domain.Users.RolePermission> RolePermissionsOf(Guid dealerId) =>
        context.RolePermissions.Where(p => context.Roles.IgnoreQueryFilters().Any(r => r.Id == p.RoleId && r.DealerId == dealerId));

    private async Task ClearClientOriginLeadsAsync(Guid dealerId, CancellationToken ct)
    {
        while (true)
        {
            List<Domain.Clients.Client> batch = await context.Clients
                .IgnoreQueryFilters()
                .Where(c => c.DealerId == dealerId && c.OriginLeadId != null)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                return;

            foreach (Domain.Clients.Client client in batch)
                client.ClearOriginLead();

            await context.SaveChangesAsync(ct);
        }
    }

    private async Task<int> DeleteAsync<T>(
        IQueryable<T> query, DbSet<T> set, Func<T, string?>? storageKey, CancellationToken ct)
        where T : class
    {
        int total = 0;
        while (true)
        {
            List<T> batch = await query.Take(BatchSize).ToListAsync(ct);
            if (batch.Count == 0)
                return total;

            if (storageKey is not null)
            {
                foreach (T row in batch)
                {
                    string? key = storageKey(row);
                    if (!string.IsNullOrWhiteSpace(key))
                        await storage.DeleteFileAsync(key, ct);
                }
            }

            set.RemoveRange(batch);
            await context.SaveChangesAsync(ct);
            total += batch.Count;
        }
    }
}
