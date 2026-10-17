using Application.AfterSales;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;
using SharedKernel;

namespace Infrastructure.BackgroundJobs;

/// <summary>sfc C2.4 / RN-049: daily after-sales sweep (service cadence and warranties about to end).</summary>
[DisallowConcurrentExecution]
public sealed class ServiceReminderJob(ISender sender, ILogger<ServiceReminderJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext jobContext)
    {
        Result<int> result = await sender.Send(new GenerateServiceRemindersCommand(), jobContext.CancellationToken);
        if (result.IsFailure)
            logger.LogWarning("After-sales sweep failed: {Error}", result.Error.Code);
        else if (result.Value > 0)
            logger.LogInformation("After-sales sweep created {Count} reminders", result.Value);

        // sfc C2.4.2: email the clients (consent permitting) once the reminders exist.
        Result<int> notified = await sender.Send(new NotifyServiceRemindersCommand(), jobContext.CancellationToken);
        if (notified.IsFailure)
            logger.LogWarning("After-sales email sweep failed: {Error}", notified.Error.Code);
        else if (notified.Value > 0)
            logger.LogInformation("After-sales sweep emailed {Count} reminders", notified.Value);
    }
}
