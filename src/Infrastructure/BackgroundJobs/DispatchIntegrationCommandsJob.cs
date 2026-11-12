using Infrastructure.Integrations;
using Quartz;

namespace Infrastructure.BackgroundJobs;

/// <summary>sfc C1.1 / ADR C22: polls <c>integration_commands</c>; all behavior lives in <see cref="IntegrationCommandDispatcher"/>.</summary>
[DisallowConcurrentExecution]
public sealed class DispatchIntegrationCommandsJob(IntegrationCommandDispatcher dispatcher) : IJob
{
    public async Task Execute(IJobExecutionContext context) =>
        await dispatcher.DispatchDueAsync(context.CancellationToken);
}
