using Application.Abstractions.Data;
using Application.Abstractions.Integrations;
using Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Infrastructure.Integrations;

/// <summary>
/// sfc C1.1 / ADR C22: drains due <see cref="IntegrationCommand"/> rows. Per command: disabled provider is skipped,
/// unknown provider dead-letters, the rate limiter can defer it (no attempt consumed), then the provider client runs;
/// a failure or exception is a retry with exponential backoff until the dead letter. Runs without tenant context.
/// </summary>
public sealed class IntegrationCommandDispatcher(
    IApplicationDbContext dbContext,
    IRateLimiter rateLimiter,
    IEnumerable<IIntegrationClient> clients,
    IIntegrationProviderSettings settings,
    IDateTimeProvider dateTimeProvider,
    ILogger<IntegrationCommandDispatcher> logger)
{
    public const int BatchSize = 20;

    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.UtcNow;

        List<IntegrationCommand> due = await dbContext.IntegrationCommands
            .IgnoreQueryFilters() // background job runs without tenant context
            .Where(c => c.Status == IntegrationCommandStatus.Pending && c.NextAttemptAtUtc <= now)
            .OrderBy(c => c.NextAttemptAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        Dictionary<string, IIntegrationClient> byProvider = clients
            .GroupBy(c => c.Provider.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        int processed = 0;
        foreach (IntegrationCommand command in due)
        {
            if (!settings.IsEnabled(command.Provider))
            {
                continue; // stays Pending; picked up once the provider is enabled
            }

            if (!byProvider.TryGetValue(command.Provider, out IIntegrationClient? client))
            {
                command.DeadLetter(dateTimeProvider.UtcNow, $"No integration client registered for provider '{command.Provider}'");
                processed++;
                continue;
            }

            RateLimitDecision decision = await rateLimiter.TryAcquireAsync(command.DealerId, command.Provider, cancellationToken);
            if (!decision.Allowed)
            {
                command.Reschedule(dateTimeProvider.UtcNow + decision.RetryAfter);
                continue;
            }

            processed++;
            await ExecuteAsync(client, command, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return processed;
    }

    private async Task ExecuteAsync(IIntegrationClient client, IntegrationCommand command, CancellationToken cancellationToken)
    {
        try
        {
            IntegrationResult result = await client.ExecuteAsync(
                new IntegrationRequest(command.Id, command.DealerId, command.Provider, command.Operation, command.Payload),
                cancellationToken);

            if (result.Succeeded)
            {
                command.RecordSuccess(dateTimeProvider.UtcNow);
            }
            else
            {
                command.RecordFailure(dateTimeProvider.UtcNow, result.Error ?? "Provider reported a failure");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Integration command {CommandId} ({Provider}/{Operation}) threw", command.Id, command.Provider, command.Operation);
            command.RecordFailure(dateTimeProvider.UtcNow, ex.Message);
        }
    }
}
