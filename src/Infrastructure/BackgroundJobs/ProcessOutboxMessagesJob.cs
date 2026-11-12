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
    DbContext efContext,
    IPublisher publisher,
    ILogger<ProcessOutboxMessagesJob> logger) : IJob
{
    // DI resolves both IApplicationDbContext and the base DbContext to the same scoped
    // ApplicationDbContext instance (same identity, two interfaces) — efContext is only here for
    // the raw Database.ExecuteSqlInterpolatedAsync/Entry access IApplicationDbContext doesn't expose.

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

    /// <summary>
    /// Security review follow-up (spr PR 5.1): the claim must never grab a row another job owns.
    /// "StripeRaw" rows belong exclusively to <see cref="ProcessStripeWebhooksJob"/> — without this
    /// filter, this job's atomic claim marks them processed before that job ever sees them, silently
    /// dropping every Stripe webhook routed through the outbox. Add any other job-owned Type here.
    /// </summary>
    private const string StripeRawType = "StripeRaw";

    public async Task Execute(IJobExecutionContext context)
    {
        // spr PR 5.1 (OPS-07): defense in depth, independent of Quartz clustering — atomically
        // claim rows via a single UPDATE ... RETURNING guarded by FOR UPDATE SKIP LOCKED, so two
        // schedulers racing against the same Postgres (e.g. the blue/green overlap window during
        // a deploy) can never read and process the same outbox message twice.
        DateTime claimedAtUtc = DateTime.UtcNow;
        List<OutboxMessage> messages = await dbContext
            .OutboxMessages
            .FromSqlInterpolated($@"
                UPDATE ""OutboxMessages""
                SET processed_on_utc = {claimedAtUtc}
                WHERE id IN (
                    SELECT id FROM ""OutboxMessages""
                    WHERE processed_on_utc IS NULL AND type <> {StripeRawType}
                    ORDER BY occurred_on_utc
                    LIMIT 20
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING *")
            .ToListAsync(context.CancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (OutboxMessage outboxMessage in messages)
        {
            // Security review follow-up: everything that can throw for THIS message — deserialization,
            // webhook enqueue, and publish — now lives inside one try, and this message's own
            // SaveChangesAsync commits right after, before the next message is even looked at. That
            // bounds a crash's damage to exactly the message in flight: every earlier message in this
            // batch is already durably committed (claim + its own WebhookDeliveries together), and the
            // one in flight gets its claim undone below. The previous shape ran deserialization/enqueue
            // outside any try (an uncaught throw there crashed the whole batch) and deferred every
            // message's WebhookDeliveries to one final SaveChangesAsync (a later message's crash lost
            // them for every earlier message too, even though those had already "succeeded").
            // Snapshot of entities newly added to the tracker before this message starts, so a
            // failure below can detach ONLY what this specific attempt added (typically its own
            // WebhookDeliveries) without disturbing the already-committed, still-tracked entities
            // from earlier messages in this same batch (ChangeTracker.Clear() would detach those too,
            // breaking their later re-use if anything referenced them again).
            HashSet<object> trackedBeforeThisMessage = efContext.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToHashSet();

            try
            {
                if (!DomainEventTypeMap.TryGetValue(outboxMessage.Type, out Type? eventType))
                {
                    logger.LogWarning(
                        "Unknown domain event type '{EventType}' in outbox message {MessageId}. Skipping.",
                        outboxMessage.Type,
                        outboxMessage.Id);

                    outboxMessage.Error = $"Unknown event type: {outboxMessage.Type}";
                    await dbContext.SaveChangesAsync(context.CancellationToken);
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

                    outboxMessage.Error = $"Deserialization returned null for type: {outboxMessage.Type}";
                    await dbContext.SaveChangesAsync(context.CancellationToken);
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

                await publisher.Publish(domainEvent, context.CancellationToken);

                // ProcessedOnUtc is already claimedAtUtc from the atomic claim above — this save only
                // persists this message's own WebhookDeliveries (and any handler side effects on the
                // same tracked DbContext), right away, not batched with the rest.
                await dbContext.SaveChangesAsync(context.CancellationToken);
            }
            catch (Exception ex)
            {
                // The claim committed on its own, as a standalone statement — undo it immediately and
                // on its own too, with CancellationToken.None so a cancelling host still gets it
                // recorded, instead of leaving the message permanently marked processed.
                try
                {
                    string sanitizedError = ex.Message.Replace("\0", string.Empty);
                    await efContext.Database.ExecuteSqlInterpolatedAsync(
                        $@"UPDATE ""OutboxMessages"" SET processed_on_utc = NULL, error = {sanitizedError} WHERE id = {outboxMessage.Id}",
                        CancellationToken.None);
                }
                catch (Exception undoEx)
                {
                    logger.LogError(undoEx,
                        "Failed to undo the processed claim for outbox message {MessageId} after a publish failure. " +
                        "It stays marked processed and will NOT be retried.",
                        outboxMessage.Id);
                }

                // Detach only what THIS attempt added (e.g. WebhookDeliveries enqueued before the
                // throw) — never ChangeTracker.Clear(), which would also detach the already-committed
                // entities from earlier messages in this same batch and break their own later use.
                foreach (var entry in efContext.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                {
                    if (!trackedBeforeThisMessage.Contains(entry.Entity))
                    {
                        entry.State = EntityState.Detached;
                    }
                }

                // Revert the entity itself to how the database actually left it (claim undone above),
                // discarding any half-made in-memory edits (e.g. Error set before a later throw).
                efContext.Entry(outboxMessage).State = EntityState.Unchanged;
            }
        }
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
