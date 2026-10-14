using Application.Leads.FollowUp;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;
using SharedKernel;

namespace Infrastructure.BackgroundJobs;

/// <summary>sfc B6.2 / RN-041: daily follow-up sweep over every dealer's open leads.</summary>
[DisallowConcurrentExecution]
public sealed class FollowUpCadenceJob(ISender sender, ILogger<FollowUpCadenceJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext jobContext)
    {
        Result<int> result = await sender.Send(new GenerateFollowUpRemindersCommand(), jobContext.CancellationToken);
        if (result.IsFailure)
            logger.LogWarning("Follow-up sweep failed: {Error}", result.Error.Code);
        else if (result.Value > 0)
            logger.LogInformation("Follow-up sweep created {Count} reminders", result.Value);
    }
}
