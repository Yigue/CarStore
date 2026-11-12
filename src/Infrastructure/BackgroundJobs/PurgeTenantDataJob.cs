using Application.Tenancy.Purge;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;
using SharedKernel;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// RN-013 (A1.4): daily platform job. Locks tenants whose grace period ended and purges the Locked ones that are due,
/// one dealer at a time. Irreversible, so it is OFF by default: unless <c>Retention:PurgeEnabled</c> is exactly
/// <c>true</c> it runs as a dry run (counts and logs, deletes nothing). Needs legal review before it is enabled.
/// </summary>
[DisallowConcurrentExecution]
public sealed class PurgeTenantDataJob(
    ISender sender,
    IConfiguration configuration,
    ILogger<PurgeTenantDataJob> logger) : IJob
{
    public const string PurgeEnabledKey = "Retention:PurgeEnabled";

    public async Task Execute(IJobExecutionContext jobContext)
    {
        bool enabled = bool.TryParse(configuration[PurgeEnabledKey], out bool value) && value;
        if (!enabled)
            logger.LogInformation("{Key} is not true: the tenant purge runs as a dry run and deletes nothing", PurgeEnabledKey);

        Result<PurgeRunSummary> result = await sender.Send(new PurgeDueTenantsCommand(DryRun: !enabled), jobContext.CancellationToken);
        if (result.IsFailure)
            logger.LogWarning("Tenant purge run failed: {Error}", result.Error.Code);
    }
}
