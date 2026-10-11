using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Domain.DealerSettings;
using Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Tenancy.Export;

/// <summary>RN-014 (A7): resolves a 15 minute presigned URL for a Ready export of the caller's dealer.</summary>
public sealed record GetTenantExportDownloadUrlQuery(Guid ExportId) : IQuery<Uri>;

internal sealed class GetTenantExportDownloadUrlQueryHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenant,
    IStorageService storage,
    IDateTimeProvider clock) : IQueryHandler<GetTenantExportDownloadUrlQuery, Uri>
{
    public static readonly TimeSpan UrlLifetime = TimeSpan.FromMinutes(15);

    public async Task<Result<Uri>> Handle(GetTenantExportDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        Guid dealerId = tenant.DealerId;

        TenantExport? export = await context.TenantExports
            .FirstOrDefaultAsync(e => e.Id == request.ExportId && e.DealerId == dealerId, cancellationToken);

        // Expiry is checked against the clock as well as the status, so a download never depends
        // on the sweep job having run; the lifecycle comes from the row, not from the cache.
        if (export is null
            || export.Status != TenantExportStatus.Ready
            || export.ObjectKey is null
            || export.ExpiresAt is null
            || export.ExpiresAt <= clock.UtcNow)
        {
            return Result.Failure<Uri>(TenantExportErrors.NotFound);
        }

        bool purged = await context.DealerSettings
            .AnyAsync(d => d.DealerId == dealerId && d.LifecycleStatus == TenantLifecycleStatus.Purged, cancellationToken);
        if (purged)
            return Result.Failure<Uri>(TenantExportErrors.NotFound);

        Uri url = await storage.GetPresignedUrlAsync(export.ObjectKey, UrlLifetime, cancellationToken);
        return url;
    }
}
