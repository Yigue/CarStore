using SharedKernel;

namespace Domain.Integrations;

public enum IntegrationCommandStatus
{
    Pending = 0,
    Done = 1,
    DeadLetter = 2
}

/// <summary>
/// sfc C1.1 / ADR C22: a unit of outbound work for an external provider, queued in <c>integration_commands</c> and
/// drained by <c>DispatchIntegrationCommandsJob</c>. Retry state is persisted on the row (like <c>WebhookDelivery</c>),
/// so the job is a stateless poller. A rate-limited command is rescheduled without consuming an attempt.
/// </summary>
public sealed class IntegrationCommand : Entity
{
    public const int MaxAttempts = 6;
    public const int MaxErrorLength = 1000;

    public string Provider { get; private set; } = string.Empty;
    public string Operation { get; private set; } = string.Empty;
    public string Payload { get; private set; } = "{}";
    public int Attempts { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public IntegrationCommandStatus Status { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    private IntegrationCommand()
    {
    }

    public static IntegrationCommand Enqueue(
        Guid dealerId, string provider, string operation, string payload, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new DomainException("Operation is required");
        }

        var command = new IntegrationCommand
        {
            Id = Guid.NewGuid(),
            Provider = DealerIntegration.NormalizeProvider(provider),
            Operation = operation.Trim(),
            Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload,
            Status = IntegrationCommandStatus.Pending,
            NextAttemptAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
        };
        command.SetDealer(dealerId);
        return command;
    }

    /// <summary>Exponential backoff after <paramref name="attemptNumber"/> failed attempts: 30s, 1m, 2m, 4m ... capped at 1h.</summary>
    public static TimeSpan GetBackoff(int attemptNumber) =>
        TimeSpan.FromSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, attemptNumber - 1))));

    public void RecordSuccess(DateTime nowUtc)
    {
        Status = IntegrationCommandStatus.Done;
        CompletedAtUtc = nowUtc;
        LastError = null;
    }

    public void RecordFailure(DateTime nowUtc, string error)
    {
        Attempts++;
        LastError = Truncate(error);
        if (Attempts >= MaxAttempts)
        {
            Status = IntegrationCommandStatus.DeadLetter;
            CompletedAtUtc = nowUtc;
            return;
        }

        NextAttemptAtUtc = nowUtc + GetBackoff(Attempts);
    }

    /// <summary>Non-retryable failure (unknown provider, ...): straight to the dead letter without waiting for retries.</summary>
    public void DeadLetter(DateTime nowUtc, string error)
    {
        Attempts++;
        LastError = Truncate(error);
        Status = IntegrationCommandStatus.DeadLetter;
        CompletedAtUtc = nowUtc;
    }

    /// <summary>Rate limited: try again later, the attempt counter is untouched.</summary>
    public void Reschedule(DateTime nextAttemptAtUtc) => NextAttemptAtUtc = nextAttemptAtUtc;

    private static string Truncate(string error) =>
        error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
}
