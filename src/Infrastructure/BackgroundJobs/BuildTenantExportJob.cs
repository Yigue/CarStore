using Application.Abstractions.Data;
using Application.Tenancy.Export;
using Domain.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// RN-014 (A7): builds the pending tenant exports one at a time and sweeps the expired ones.
/// <see cref="DisallowConcurrentExecutionAttribute"/> keeps a long build from overlapping with the next tick.
/// </summary>
[DisallowConcurrentExecution]
public sealed class BuildTenantExportJob(
    IApplicationDbContext context,
    ISender sender,
    ILogger<BuildTenantExportJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext jobContext)
    {
        CancellationToken ct = jobContext.CancellationToken;

        await sender.Send(new ExpireTenantExportsCommand(), ct);

        // Global job: the pending queue spans every tenant, hence IgnoreQueryFilters.
        List<Guid> pending = await context.TenantExports
            .IgnoreQueryFilters()
            .Where(e => e.Status == TenantExportStatus.Requested)
            .OrderBy(e => e.RequestedAtUtc)
            .Select(e => e.Id)
            .ToListAsync(ct);

        foreach (Guid exportId in pending)
        {
            var result = await sender.Send(new BuildTenantExportCommand(exportId), ct);
            if (result.IsFailure)
                logger.LogWarning("Tenant export {ExportId} did not complete: {Error}", exportId, result.Error.Code);
        }
    }
}
