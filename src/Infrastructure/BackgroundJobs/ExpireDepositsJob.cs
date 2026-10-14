using Application.Deposits.Expire;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;
using SharedKernel;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// sfc B1.3.3 / RN-002 / RN-003: hourly deposit sweep. Runs platform-wide (no tenant), expires due deposits, releases
/// cars nothing else holds, and sends the one-time 24 h expiring notice. No money moves on expiry.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ExpireDepositsJob(ISender sender, ILogger<ExpireDepositsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext jobContext)
    {
        Result<DepositExpiryRunSummary> result = await sender.Send(new ExpireDueDepositsCommand(), jobContext.CancellationToken);
        if (result.IsFailure)
            logger.LogWarning("Deposit sweep failed: {Error}", result.Error.Code);
    }
}
