using SharedKernel;

namespace Domain.Webhooks;

/// <summary>
/// One delivery attempt lifecycle for a single (subscription, event) pair.
/// Persistence-based retry state — <see cref="NextRetryAtUtc"/> and <see cref="AttemptCount"/>
/// survive process restarts, so the dispatcher job is a pure "pick up what's due" poller.
/// Tenant-scoped via <see cref="Entity.DealerId"/> (denormalized from the subscription,
/// mirrors the <c>OutboxMessage.DealerId</c> routing column — ADR-3).
/// </summary>
public sealed class WebhookDelivery : Entity
{
    public Guid SubscriptionId { get; private set; }

    /// <summary>The originating <c>OutboxMessage.Id</c> — guards against double-enqueue when the
    /// outbox processor re-reads an unprocessed message.</summary>
    public Guid EventId { get; private set; }

    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public int AttemptCount { get; private set; }
    public WebhookDeliveryStatus Status { get; private set; }
    public DateTime NextRetryAtUtc { get; private set; }
    public int? LastStatusCode { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    // Required by EF Core
    private WebhookDelivery() { }

    public static WebhookDelivery Create(
        Guid dealerId,
        Guid subscriptionId,
        Guid eventId,
        string eventType,
        string payload,
        DateTime createdAtUtc)
    {
        var delivery = new WebhookDelivery();
        delivery.SetDealer(dealerId);
        delivery.Id = Guid.NewGuid();
        delivery.SubscriptionId = subscriptionId;
        delivery.EventId = eventId;
        delivery.EventType = eventType;
        delivery.Payload = payload;
        delivery.AttemptCount = 0;
        delivery.Status = WebhookDeliveryStatus.Pending;
        delivery.NextRetryAtUtc = createdAtUtc;
        delivery.CreatedAtUtc = createdAtUtc;

        return delivery;
    }

    public void RecordSuccess(DateTime nowUtc, int statusCode)
    {
        Status = WebhookDeliveryStatus.Delivered;
        DeliveredAtUtc = nowUtc;
        LastStatusCode = statusCode;
        LastError = null;
    }

    public void RecordFailure(DateTime nowUtc, int? statusCode, string error)
    {
        AttemptCount++;
        LastStatusCode = statusCode;
        LastError = error;

        Status = AttemptCount >= WebhookRetryPolicy.MaxAttempts
            ? WebhookDeliveryStatus.DeadLettered
            : WebhookDeliveryStatus.Pending;

        NextRetryAtUtc = nowUtc + WebhookRetryPolicy.GetBackoff(AttemptCount);
    }
}
