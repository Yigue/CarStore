using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tenancy.Export;

/// <summary>RN-014 (A7): sweeps Ready exports past their expiry and removes the stored ZIP. Platform-wide.</summary>
public sealed record ExpireTenantExportsCommand : ICommand<int>;

internal sealed class ExpireTenantExportsCommandHandler(
    IApplicationDbContext context,
    IStorageService storage,
    IDateTimeProvider clock) : ICommandHandler<ExpireTenantExportsCommand, int>
{
    public async Task<Result<int>> Handle(ExpireTenantExportsCommand request, CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow;

        // Runs outside any tenant (Quartz): the sweep spans every dealer by design.
        List<TenantExport> due = await context.TenantExports
            .IgnoreQueryFilters()
            .Where(e => e.Status == TenantExportStatus.Ready && e.ExpiresAt != null && e.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (TenantExport export in due)
        {
            if (export.ObjectKey is not null)
                await storage.DeleteFileAsync(export.ObjectKey, cancellationToken);

            export.Expire(now);
        }

        if (due.Count > 0)
            await context.SaveChangesAsync(cancellationToken);

        return due.Count;
    }
}
