using System.Data;
using Application.Abstractions.Data;
using Domain.Shared;
using Domain.Webhooks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quartz;

using SharedKernel;

namespace Infrastructure.BackgroundJobs;

[DisallowConcurrentExecution]
public class ProcessOutboxMessagesJob(
    IApplicationDbContext dbContext,
    IPublisher publisher,
    ILogger<ProcessOutboxMessagesJob> logger) : IJob
{
    /// <summary>
    /// Explicit type map for safe deserialization of domain events.
    /// Only types in this dictionary can be instantiated from outbox messages.
    /// This eliminates the RCE vector from TypeNameHandling.All.
    /// </summary>
    private static readonly Dictionary<string, Type> DomainEventTypeMap =
        // Concrete domain events live in the Domain assembly, while IDomainEvent
        // lives in SharedKernel. Scanning only IDomainEvent's assembly left the map
        // empty, so every outbox message failed with "Unknown event type" and no
        // domain-event handler ever ran. Scan both assemblies.
        new[] { typeof(IDomainEvent).Assembly, typeof(Domain.Quotes.Quote).Assembly }
            .Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First());

    private static readonly JsonSerializerSettings SafeSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None
    };

    public async Task Execute(IJobExecutionContext context)
    {
        List<OutboxMessage> messages = await dbContext
            .OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .Take(20)
            .ToListAsync(context.CancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (OutboxMessage outboxMessage in messages)
        {
            if (!DomainEventTypeMap.TryGetValue(outboxMessage.Type, out Type? eventType))
            {
                logger.LogWarning(
                    "Unknown domain event type '{EventType}' in outbox message {MessageId}. Skipping.",
                    outboxMessage.Type,
                    outboxMessage.Id);

                outboxMessage.ProcessedOnUtc = DateTime.UtcNow;
                outboxMessage.Error = $"Unknown event type: {outboxMessage.Type}";
                continue;
            }

            IDomainEvent? domainEvent = (IDomainEvent?)JsonConvert
                .DeserializeObject(
                    outboxMessage.Content,
                    eventType,
                    SafeSerializerSettings);

            if (domainEvent is null)
            {
                logger.LogWarning(
                    "Failed to deserialize outbox message {MessageId} as {EventType}. Skipping.",
                    outboxMessage.Id,
                    outboxMessage.Type);

                outboxMessage.ProcessedOnUtc = DateTime.UtcNow;
                outboxMessage.Error = $"Deserialization returned null for type: {outboxMessage.Type}";
                continue;
            }

            // Outgoing webhooks: fan out to tenant subscriptions before publishing so a
            // failing in-process MediatR handler never blocks external delivery. Enqueue
            // is idempotent (checked against WebhookDelivery.EventId) so re-reads of a
            // still-unprocessed message on the next tick never double-enqueue.
            if (outboxMessage.DealerId is Guid dealerId)
            {
                if (WebhookEventCatalog.FromDomainEventTypeName.TryGetValue(outboxMessage.Type, out string? catalogEventType))
                {
                    await EnqueueWebhookDeliveriesAsync(dealerId, outboxMessage, catalogEventType, context.CancellationToken);
                }
            }
            else if (WebhookEventCatalog.FromDomainEventTypeName.ContainsKey(outboxMessage.Type))
            {
                logger.LogWarning(
                    "Outbox message {MessageId} of type {EventType} is webhook-catalogued but has no DealerId — skipping webhook fan-out.",
                    outboxMessage.Id,
                    outboxMessage.Type);
            }

            try
            {
                await publisher.Publish(domainEvent, context.CancellationToken);

                outboxMessage.ProcessedOnUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                outboxMessage.Error = ex.Message;
            }
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);
    }

    /// <remarks>internal (not private) so ApplicationTests (Infrastructure InternalsVisibleTo target)
    /// can exercise the enqueue/idempotency/tenant-filtering logic without standing up a Quartz
    /// IJobExecutionContext.</remarks>
    internal async Task EnqueueWebhookDeliveriesAsync(
        Guid dealerId,
        OutboxMessage outboxMessage,
        string eventType,
        CancellationToken cancellationToken)
    {
        List<WebhookSubscription> subscriptions = await dbContext.WebhookSubscriptions
            .IgnoreQueryFilters() // background job runs without tenant context
            .Where(s => s.DealerId == dealerId && s.IsActive)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        List<WebhookSubscription> matching = subscriptions
            .Where(s => s.IsSubscribedTo(eventType))
            .ToList();

        if (matching.Count == 0)
        {
            return;
        }

        List<Guid> subscriptionIds = matching.Select(s => s.Id).ToList();

        HashSet<Guid> alreadyEnqueued = (await dbContext.WebhookDeliveries
                .IgnoreQueryFilters()
                .Where(d => d.EventId == outboxMessage.Id && subscriptionIds.Contains(d.SubscriptionId))
                .Select(d => d.SubscriptionId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        // Envelope wraps the already-serialized domain event (JRaw) so the wire payload is
        // stable and independent of how the outbox stored it.
        string payload = JsonConvert.SerializeObject(new
        {
            @event = eventType,
            occurredAt = outboxMessage.OccurredOnUtc,
            dealerId,
            data = new JRaw(outboxMessage.Content)
        });

        DateTime now = DateTime.UtcNow;

        foreach (WebhookSubscription subscription in matching)
        {
            if (alreadyEnqueued.Contains(subscription.Id))
            {
                continue;
            }

            WebhookDelivery delivery = WebhookDelivery.Create(
                dealerId,
                subscription.Id,
                outboxMessage.Id,
                eventType,
                payload,
                now);

            dbContext.WebhookDeliveries.Add(delivery);
        }
    }
}
