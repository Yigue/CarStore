namespace Domain.Webhooks;

public enum WebhookDeliveryStatus
{
    /// <summary>Never attempted, or awaiting its next scheduled retry.</summary>
    Pending = 0,

    /// <summary>Delivered successfully (2xx response).</summary>
    Delivered = 1,

    /// <summary>Exhausted <see cref="WebhookRetryPolicy.MaxAttempts"/> without success.</summary>
    DeadLettered = 2,
}
