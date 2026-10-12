using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Domain.Billing;
using Domain.Shared;
using FluentAssertions;
using Infrastructure.BackgroundJobs;
using Infrastructure.Billing;
using Infrastructure.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebApiTests;
using Xunit;

namespace WebApiTests.IntegrationTests.Billing;

public class ProcessStripeWebhooksJobTests
{
    [Fact]
    public async Task Execute_ProcessesMultipleEventsInOrder_AndSkipsDuplicates()
    {
        // Arrange
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var repo = scope.ServiceProvider.GetRequiredService<IDealerSubscriptionRepository>();
        var procRepo = scope.ServiceProvider.GetRequiredService<ProcessedStripeEventRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var logger = NullLogger<ProcessStripeWebhooksJob>.Instance;

        var job = new ProcessStripeWebhooksJob(dbContext, repo, procRepo, sender, logger);

        var dealerId = Guid.NewGuid();

        // Event 1: customer.subscription.created with status "active"
        // This creates the subscription and activates it (Trialing → Active)
        var payload1 = $$"""
        {
            "id": "evt_1",
            "type": "customer.subscription.created",
            "data": {
                "object": {
                    "id": "sub_123",
                    "customer": "cus_123",
                    "status": "active",
                    "current_period_start": 1700000000,
                    "current_period_end": 1701209600,
                    "trial_end": null,
                    "items": {
                        "data": [
                            {
                                "price": {
                                    "id": "price_123"
                                }
                            }
                        ]
                    },
                    "metadata": {
                        "dealer_id": "{{dealerId}}"
                    }
                }
            }
        }
        """;

        // Event 2: invoice.payment_failed → MarkPastDue (Active → PastDue)
        var payload2 = """
        {
            "id": "evt_2",
            "type": "invoice.payment_failed",
            "data": {
                "object": {
                    "customer": "cus_123",
                    "subscription": "sub_123"
                }
            }
        }
        """;

        // Event 3: duplicate of event 1 — should be skipped via idempotency guard
        var payload1Dup = payload1;

        var msg1 = new OutboxMessage { Id = Guid.NewGuid(), Type = "StripeRaw", Content = payload1, OccurredOnUtc = DateTime.UtcNow };
        var msg2 = new OutboxMessage { Id = Guid.NewGuid(), Type = "StripeRaw", Content = payload2, OccurredOnUtc = DateTime.UtcNow.AddSeconds(1) };
        var msg3 = new OutboxMessage { Id = Guid.NewGuid(), Type = "StripeRaw", Content = payload1Dup, OccurredOnUtc = DateTime.UtcNow.AddSeconds(2) };

        dbContext.OutboxMessages.AddRange(msg1, msg2, msg3);
        await dbContext.SaveChangesAsync();

        // Act
        var mockContext = new Mock<IJobExecutionContext>();
        await job.Execute(mockContext.Object);

        // Assert
        var subscription = await dbContext.DealerSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.DealerId == dealerId);

        subscription.Should().NotBeNull("event 1 should create a DealerSubscription via customer.subscription.created");
        subscription!.Status.Should().Be(SubscriptionStatus.PastDue,
            "event 2 (invoice.payment_failed) transitions Active → PastDue");

        var processedEvents = await dbContext.ProcessedStripeEvents.ToListAsync();
        processedEvents.Select(e => e.StripeEventId).Should().BeEquivalentTo(new[] { "evt_1", "evt_2" },
            "duplicate evt_1 (msg3) should be skipped by idempotency guard");

        var unprocessedCount = await dbContext.OutboxMessages.CountAsync(m => m.Type == "StripeRaw" && m.ProcessedOnUtc == null);
        unprocessedCount.Should().Be(0, "all 3 outbox messages should be marked as processed");
    }

    private const long BaselineUnix = 1700000000;
    private const long PaymentFailedUnix = 1700001000;

    private static readonly string PaymentFailedPayload = $$"""
    {
        "id": "evt_fail_1",
        "type": "invoice.payment_failed",
        "created": {{PaymentFailedUnix}},
        "data": { "object": { "customer": "cus_fail", "subscription": "sub_fail" } }
    }
    """;

    private static DateTime FromUnix(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;

    private static async Task<DealerSubscription> SeedActiveSubscriptionAsync(ApplicationDbContext dbContext)
    {
        var sub = DealerSubscription.Create(Guid.NewGuid(), "cus_fail", "sub_fail", "price_1");
        sub.Activate("sub_fail", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        sub.TryRegisterProviderEvent(FromUnix(BaselineUnix)).Should().BeTrue();
        dbContext.DealerSubscriptions.Add(sub);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return sub;
    }

    private static OutboxMessage RawMessage(string payload) =>
        new() { Id = Guid.NewGuid(), Type = "StripeRaw", Content = payload, OccurredOnUtc = DateTime.UtcNow };

    /// <summary>Runs the real command, then throws: the state change and watermark are already tracked when it fails.</summary>
    private static ISender SenderThatFailsAfterApplying(ISender real)
    {
        var failing = new Mock<ISender>();
        failing
            .Setup(s => s.Send(It.IsAny<MediatR.IRequest<SharedKernel.Result>>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(async (MediatR.IRequest<SharedKernel.Result> request, System.Threading.CancellationToken ct) =>
            {
                await real.Send(request, ct);
                throw new InvalidOperationException("boom after apply");
            });
        return failing.Object;
    }

    [Fact]
    public async Task Execute_WhenApplyFailsMidway_DoesNotPersistWatermarkStateOrIdempotency_AndRedeliveryIsApplied()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var seeded = await SeedActiveSubscriptionAsync(dbContext);
        var repo = scope.ServiceProvider.GetRequiredService<IDealerSubscriptionRepository>();
        var procRepo = scope.ServiceProvider.GetRequiredService<ProcessedStripeEventRepository>();
        var real = scope.ServiceProvider.GetRequiredService<ISender>();

        var failedMsg = RawMessage(PaymentFailedPayload);
        dbContext.OutboxMessages.Add(failedMsg);
        await dbContext.SaveChangesAsync();

        var failingJob = new ProcessStripeWebhooksJob(dbContext, repo, procRepo, SenderThatFailsAfterApplying(real), NullLogger<ProcessStripeWebhooksJob>.Instance);
        await failingJob.Execute(new Mock<IJobExecutionContext>().Object);

        var afterFailure = await dbContext.DealerSubscriptions.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == seeded.DealerId);
        afterFailure.LastProviderEventAtUtc.Should().Be(FromUnix(BaselineUnix), "a failed apply must not advance the watermark");
        afterFailure.Status.Should().Be(SubscriptionStatus.Active, "a failed apply must not change state");

        (await dbContext.ProcessedStripeEvents.AsNoTracking().CountAsync())
            .Should().Be(0, "the idempotency row of a failed message must not be persisted");

        var storedFailed = await dbContext.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == failedMsg.Id);
        storedFailed.Error.Should().NotBeNullOrEmpty("the failure must stay visible on the outbox message");

        // Stripe redelivers the same event: it must be applied, not discarded as out-of-order or duplicate.
        dbContext.OutboxMessages.Add(RawMessage(PaymentFailedPayload));
        await dbContext.SaveChangesAsync();

        var okJob = new ProcessStripeWebhooksJob(dbContext, repo, procRepo, real, NullLogger<ProcessStripeWebhooksJob>.Instance);
        await okJob.Execute(new Mock<IJobExecutionContext>().Object);

        var afterRedelivery = await dbContext.DealerSubscriptions.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == seeded.DealerId);
        afterRedelivery.Status.Should().Be(SubscriptionStatus.PastDue);
        afterRedelivery.LastProviderEventAtUtc.Should().Be(FromUnix(PaymentFailedUnix));
        (await dbContext.ProcessedStripeEvents.AsNoTracking().Select(e => e.StripeEventId).ToListAsync())
            .Should().BeEquivalentTo(new[] { "evt_fail_1" });
    }

    [Fact]
    public async Task Execute_WhenOneMessageFails_LaterMessagesAreStillProcessed()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var seeded = await SeedActiveSubscriptionAsync(dbContext);
        var repo = scope.ServiceProvider.GetRequiredService<IDealerSubscriptionRepository>();
        var procRepo = scope.ServiceProvider.GetRequiredService<ProcessedStripeEventRepository>();
        var real = scope.ServiceProvider.GetRequiredService<ISender>();

        var first = RawMessage(PaymentFailedPayload);
        var second = RawMessage("""{ "id": "evt_other", "type": "charge.refunded", "created": 1700002000, "data": { "object": { "customer": "cus_fail" } } }""");
        second.OccurredOnUtc = first.OccurredOnUtc.AddSeconds(1);
        dbContext.OutboxMessages.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        var calls = 0;
        var flaky = new Mock<ISender>();
        flaky
            .Setup(s => s.Send(It.IsAny<MediatR.IRequest<SharedKernel.Result>>(), It.IsAny<System.Threading.CancellationToken>()))
            .Returns(async (MediatR.IRequest<SharedKernel.Result> request, System.Threading.CancellationToken ct) =>
            {
                var result = await real.Send(request, ct);
                if (calls++ == 0)
                {
                    throw new InvalidOperationException("boom after apply");
                }

                return result;
            });

        var job = new ProcessStripeWebhooksJob(dbContext, repo, procRepo, flaky.Object, NullLogger<ProcessStripeWebhooksJob>.Instance);
        await job.Execute(new Mock<IJobExecutionContext>().Object);

        var stored = await dbContext.OutboxMessages.AsNoTracking().Where(m => m.Type == "StripeRaw").ToListAsync();
        stored.Single(m => m.Id == first.Id).Error.Should().NotBeNullOrEmpty();
        stored.Single(m => m.Id == second.Id).Error.Should().BeNull();
        stored.Should().OnlyContain(m => m.ProcessedOnUtc != null);
        (await dbContext.ProcessedStripeEvents.AsNoTracking().Select(e => e.StripeEventId).ToListAsync())
            .Should().BeEquivalentTo(new[] { "evt_other" });
    }

    [Fact]
    public async Task Execute_WhenApplySucceeds_AdvancesWatermarkAndPersistsState()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var seeded = await SeedActiveSubscriptionAsync(dbContext);
        var repo = scope.ServiceProvider.GetRequiredService<IDealerSubscriptionRepository>();
        var procRepo = scope.ServiceProvider.GetRequiredService<ProcessedStripeEventRepository>();
        var real = scope.ServiceProvider.GetRequiredService<ISender>();

        dbContext.OutboxMessages.Add(RawMessage(PaymentFailedPayload));
        await dbContext.SaveChangesAsync();

        var job = new ProcessStripeWebhooksJob(dbContext, repo, procRepo, real, NullLogger<ProcessStripeWebhooksJob>.Instance);
        await job.Execute(new Mock<IJobExecutionContext>().Object);

        var sub = await dbContext.DealerSubscriptions.AsNoTracking().IgnoreQueryFilters()
            .SingleAsync(s => s.DealerId == seeded.DealerId);
        sub.Status.Should().Be(SubscriptionStatus.PastDue);
        sub.LastProviderEventAtUtc.Should().Be(FromUnix(PaymentFailedUnix));
        (await dbContext.OutboxMessages.AsNoTracking().SingleAsync(m => m.Type == "StripeRaw")).Error.Should().BeNull();
    }
}
