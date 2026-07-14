using Application.UnitTests;
using Domain.Shared;
using Domain.Webhooks;
using Infrastructure.BackgroundJobs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Webhooks;

/// <summary>
/// Covers the outbox-processor-side of outgoing webhooks: enqueueing WebhookDelivery rows
/// for matching, active, tenant-scoped subscriptions, and staying idempotent across re-reads
/// of the same still-unprocessed OutboxMessage (see EnqueueWebhookDeliveriesAsync in
/// ProcessOutboxMessagesJob). Exercises the method directly — Execute() needs a Quartz
/// IJobExecutionContext, which is unnecessary ceremony for this behavior.
/// </summary>
public class ProcessOutboxMessagesJobWebhookTests
{
    private static readonly Guid DealerA = Guid.NewGuid();
    private static readonly Guid DealerB = Guid.NewGuid();

    private TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestApplicationDbContext(options);
    }

    private static ProcessOutboxMessagesJob CreateJob(TestApplicationDbContext context) =>
        new(context, Mock.Of<IPublisher>(), NullLogger<ProcessOutboxMessagesJob>.Instance);

    private static OutboxMessage CreateOutboxMessage(Guid dealerId) => new()
    {
        Id = Guid.NewGuid(),
        Type = "SaleCreatedDomainEvent",
        Content = "{}",
        OccurredOnUtc = DateTime.UtcNow,
        DealerId = dealerId,
    };

    [Fact]
    public async Task EnqueueWebhookDeliveries_ShouldCreateDelivery_ForActiveMatchingSubscription()
    {
        var context = CreateContext();
        var subscription = WebhookSubscription.Create(
            DealerA, "https://example.com/hook", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);
        context.WebhookSubscriptions.Add(subscription);
        context.SaveChanges();

        var job = CreateJob(context);
        var outboxMessage = CreateOutboxMessage(DealerA);

        await job.EnqueueWebhookDeliveriesAsync(DealerA, outboxMessage, WebhookEventCatalog.SaleCreated, CancellationToken.None);
        await context.SaveChangesAsync();

        var deliveries = await context.WebhookDeliveries.ToListAsync();
        deliveries.Should().ContainSingle();
        deliveries[0].SubscriptionId.Should().Be(subscription.Id);
        deliveries[0].EventId.Should().Be(outboxMessage.Id);
        deliveries[0].EventType.Should().Be(WebhookEventCatalog.SaleCreated);
    }

    [Fact]
    public async Task EnqueueWebhookDeliveries_ShouldSkip_SubscriptionsFromOtherDealers()
    {
        var context = CreateContext();
        var subscription = WebhookSubscription.Create(
            DealerB, "https://example.com/hook", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);
        context.WebhookSubscriptions.Add(subscription);
        context.SaveChanges();

        var job = CreateJob(context);
        var outboxMessage = CreateOutboxMessage(DealerA);

        // Dealer A's event — Dealer B's subscription must not receive it.
        await job.EnqueueWebhookDeliveriesAsync(DealerA, outboxMessage, WebhookEventCatalog.SaleCreated, CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.WebhookDeliveries.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EnqueueWebhookDeliveries_ShouldSkip_InactiveSubscription()
    {
        var context = CreateContext();
        var subscription = WebhookSubscription.Create(
            DealerA, "https://example.com/hook", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);
        subscription.UpdateDetails(subscription.Url, subscription.EventTypes, isActive: false);
        context.WebhookSubscriptions.Add(subscription);
        context.SaveChanges();

        var job = CreateJob(context);
        var outboxMessage = CreateOutboxMessage(DealerA);

        await job.EnqueueWebhookDeliveriesAsync(DealerA, outboxMessage, WebhookEventCatalog.SaleCreated, CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.WebhookDeliveries.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EnqueueWebhookDeliveries_ShouldSkip_SubscriptionNotSubscribedToEventType()
    {
        var context = CreateContext();
        var subscription = WebhookSubscription.Create(
            DealerA, "https://example.com/hook", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.LeadStatusChanged], DateTime.UtcNow);
        context.WebhookSubscriptions.Add(subscription);
        context.SaveChanges();

        var job = CreateJob(context);
        var outboxMessage = CreateOutboxMessage(DealerA);

        await job.EnqueueWebhookDeliveriesAsync(DealerA, outboxMessage, WebhookEventCatalog.SaleCreated, CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.WebhookDeliveries.ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task EnqueueWebhookDeliveries_ShouldBeIdempotent_AcrossRepeatedCallsForSameEvent()
    {
        var context = CreateContext();
        var subscription = WebhookSubscription.Create(
            DealerA, "https://example.com/hook", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);
        context.WebhookSubscriptions.Add(subscription);
        context.SaveChanges();

        var job = CreateJob(context);
        var outboxMessage = CreateOutboxMessage(DealerA);

        // Simulates the outbox processor re-reading the same still-unprocessed message
        // on a later tick (e.g. because MediatR.Publish threw for an unrelated handler).
        await job.EnqueueWebhookDeliveriesAsync(DealerA, outboxMessage, WebhookEventCatalog.SaleCreated, CancellationToken.None);
        await context.SaveChangesAsync();

        await job.EnqueueWebhookDeliveriesAsync(DealerA, outboxMessage, WebhookEventCatalog.SaleCreated, CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.WebhookDeliveries.ToListAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task EnqueueWebhookDeliveries_ShouldFanOut_ToMultipleMatchingSubscriptions()
    {
        var context = CreateContext();
        var sub1 = WebhookSubscription.Create(
            DealerA, "https://example.com/hook1", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);
        var sub2 = WebhookSubscription.Create(
            DealerA, "https://example.com/hook2", "fedcba9876543210fedcba9876543210",
            [WebhookEventCatalog.SaleCreated, WebhookEventCatalog.LeadStatusChanged], DateTime.UtcNow);
        context.WebhookSubscriptions.AddRange(sub1, sub2);
        context.SaveChanges();

        var job = CreateJob(context);
        var outboxMessage = CreateOutboxMessage(DealerA);

        await job.EnqueueWebhookDeliveriesAsync(DealerA, outboxMessage, WebhookEventCatalog.SaleCreated, CancellationToken.None);
        await context.SaveChangesAsync();

        (await context.WebhookDeliveries.ToListAsync()).Should().HaveCount(2);
    }
}
