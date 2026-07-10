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
}
