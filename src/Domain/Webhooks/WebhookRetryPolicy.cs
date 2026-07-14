namespace Domain.Webhooks;

/// <summary>
/// Retry/backoff schedule for outgoing webhook deliveries: 1m / 5m / 30m / 2h,
/// dead-lettered after the 5th failed attempt.
/// </summary>
public static class WebhookRetryPolicy
{
    public const int MaxAttempts = 5;

    private static readonly TimeSpan[] BackoffByAttempt =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
    ];

    /// <summary>
    /// Delay to schedule the next retry after <paramref name="attemptNumber"/> (1-based,
    /// the count of attempts made so far, including the one that just failed).
    /// </summary>
    public static TimeSpan GetBackoff(int attemptNumber) =>
        attemptNumber >= 1 && attemptNumber <= BackoffByAttempt.Length
            ? BackoffByAttempt[attemptNumber - 1]
            : TimeSpan.Zero;
}
