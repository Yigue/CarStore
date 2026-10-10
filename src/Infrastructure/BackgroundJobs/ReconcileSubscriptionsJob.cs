using Application.Billing.Commands.ReconcileSubscriptions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// spr ADR B9: periodic subscription reconciliation. Thin wrapper over
/// <see cref="ReconcileSubscriptionsCommand"/> (the testable logic); scheduling comes from
/// <c>Billing:Reconciliation</c>. Never deletes — see the handler.
/// </summary>
[DisallowConcurrentExecution]
public sealed class ReconcileSubscriptionsJob(
    ISender sender,
    IOptions<BillingReconciliationOptions> options,
    ILogger<ReconcileSubscriptionsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        BillingReconciliationOptions settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        var result = await sender.Send(
            new ReconcileSubscriptionsCommand(TimeSpan.FromHours(settings.GraceHours), settings.BatchSize),
            context.CancellationToken);

        if (result.IsFailure)
        {
            logger.LogError("Billing reconciliation run failed: {Error}", result.Error.Description);
            return;
        }

        logger.LogInformation(
            "Billing reconciliation: examined {Examined}, healed {Healed}, flagged {Flagged}, cleared {Cleared}.",
            result.Value.Examined, result.Value.Healed, result.Value.Flagged, result.Value.Cleared);
    }
}
