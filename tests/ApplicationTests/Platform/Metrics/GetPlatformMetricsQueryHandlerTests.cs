using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Application.Platform.Metrics.GetPlatformMetrics;
using Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Platform.Metrics;

/// <summary>
/// spr PR 1.3 (SPR-04/FE-SA-MET-02): MRR used to be a hardcoded stub (<c>isStub:true</c>). It now sums
/// the amount of every Active/Trialing/PastDue subscription's plan — Cancelled/Suspended/Incomplete
/// never contribute, and the real (possibly zero) result is never reported as a stub.
/// </summary>
public class GetPlatformMetricsQueryHandlerTests
{
    private const string PlanCode = "standard";

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Domain.DealerSettings.DealerSettings SeedDealer(
        TestApplicationDbContext context,
        string name = "Dealer",
        bool isActive = true)
    {
        var ds = new Domain.DealerSettings.DealerSettings(Guid.NewGuid(), name, $"{name.ToLower()}@dealer.com");
        if (!isActive)
            ds.Suspend("test suspension", Guid.NewGuid(), DateTime.UtcNow);

        context.DealerSettings.Add(ds);
        context.SaveChanges();
        ds.ClearDomainEvents();
        return ds;
    }

    private static DealerSubscription SeedSubscription(
        TestApplicationDbContext context,
        SubscriptionStatus status,
        string planCode = PlanCode)
    {
        var dealerId = Guid.NewGuid();

        // Create() always starts at Trialing; CreatePending() is the only factory that starts at
        // Incomplete (spr ADR B8 — provisioned but no confirmed payment yet).
        var subscription = status == SubscriptionStatus.Incomplete
            ? DealerSubscription.CreatePending(dealerId, $"cus_{dealerId:N}", "price_mock", planCode: planCode)
            : DealerSubscription.Create(dealerId, $"cus_{dealerId:N}", $"sub_{dealerId:N}", "price_mock", planCode: planCode);

        // Drive the subscription into the requested status via its own transitions, so the test
        // exercises the same invariants the real aggregate enforces.
        switch (status)
        {
            case SubscriptionStatus.Trialing:
            case SubscriptionStatus.Incomplete:
                break;
            case SubscriptionStatus.Active:
                subscription.Activate("sub_x", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
                break;
            case SubscriptionStatus.PastDue:
                subscription.Activate("sub_x", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
                subscription.MarkPastDue();
                break;
            case SubscriptionStatus.Suspended:
                subscription.Suspend();
                break;
            case SubscriptionStatus.Cancelled:
                subscription.Cancel();
                break;
        }

        context.DealerSubscriptions.Add(subscription);
        context.SaveChanges();
        subscription.ClearDomainEvents();
        return subscription;
    }

    private static Mock<IPlanCatalog> CreatePlanCatalogMock(long amountMinor = 10000, string currency = "usd")
    {
        var planCatalog = new Mock<IPlanCatalog>();
        planCatalog
            .Setup(p => p.GetAsync(PlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanDetails(PlanCode, "price_mock", amountMinor, currency, "month", 14));
        return planCatalog;
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsZeroCounts()
    {
        using var context = CreateContext();
        var handler = new GetPlatformMetricsQueryHandler(context, CreatePlanCatalogMock().Object);

        var result = await handler.Handle(new GetPlatformMetricsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDealers.Should().Be(0);
        result.Value.ActiveDealers.Should().Be(0);
        result.Value.SuspendedDealers.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MixedDealers_ReturnsCorrectCounts()
    {
        using var context = CreateContext();
        SeedDealer(context, "Alpha", isActive: true);
        SeedDealer(context, "Beta", isActive: true);
        SeedDealer(context, "Gamma", isActive: false);

        var handler = new GetPlatformMetricsQueryHandler(context, CreatePlanCatalogMock().Object);
        var result = await handler.Handle(new GetPlatformMetricsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDealers.Should().Be(3);
        result.Value.ActiveDealers.Should().Be(2);
        result.Value.SuspendedDealers.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NoSubscriptions_ReturnsRealZero_NotStub()
    {
        using var context = CreateContext();
        var handler = new GetPlatformMetricsQueryHandler(context, CreatePlanCatalogMock().Object);

        var result = await handler.Handle(new GetPlatformMetricsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Mrr.Value.Should().Be(0m);
        result.Value.Mrr.IsStub.Should().BeFalse();
        result.Value.Mrr.StubReason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ActiveTrialingAndPastDueSubscriptions_AreSummedIntoMrr()
    {
        using var context = CreateContext();
        SeedSubscription(context, SubscriptionStatus.Active);
        SeedSubscription(context, SubscriptionStatus.Trialing);
        SeedSubscription(context, SubscriptionStatus.PastDue);

        // $100.00 per subscription (10000 minor units) × 3 contributing subscriptions.
        var handler = new GetPlatformMetricsQueryHandler(context, CreatePlanCatalogMock(10000, "usd").Object);
        var result = await handler.Handle(new GetPlatformMetricsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Mrr.Value.Should().Be(300m);
        result.Value.Mrr.Currency.Should().Be("usd");
        result.Value.Mrr.IsStub.Should().BeFalse();
        result.Value.Mrr.StubReason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CancelledSuspendedAndIncompleteSubscriptions_DoNotContributeToMrr()
    {
        using var context = CreateContext();
        SeedSubscription(context, SubscriptionStatus.Active);
        SeedSubscription(context, SubscriptionStatus.Cancelled);
        SeedSubscription(context, SubscriptionStatus.Suspended);
        SeedSubscription(context, SubscriptionStatus.Incomplete);

        var handler = new GetPlatformMetricsQueryHandler(context, CreatePlanCatalogMock(10000, "usd").Object);
        var result = await handler.Handle(new GetPlatformMetricsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Mrr.Value.Should().Be(100m);
        result.Value.Mrr.IsStub.Should().BeFalse();
    }
}
