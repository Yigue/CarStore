using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Platform.Dealers.GetAllDealers;
using Domain.Billing;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Platform.Dealers;

public class GetAllDealersQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Domain.DealerSettings.DealerSettings SeedDealer(
        TestApplicationDbContext context,
        Guid? dealerId = null,
        string name = "Dealer",
        bool isActive = true)
    {
        var ds = new Domain.DealerSettings.DealerSettings(
            dealerId ?? Guid.NewGuid(),
            name,
            $"{name.ToLower()}@dealer.com");

        if (!isActive)
        {
            ds.Suspend("Test suspension", Guid.NewGuid(), DateTime.UtcNow);
        }

        context.DealerSettings.Add(ds);
        context.SaveChanges();
        ds.ClearDomainEvents();
        return ds;
    }

    [Fact]
    public async Task Handle_ReturnsAllDealers_CrossTenant()
    {
        using var context = CreateContext();

        // Seed 3 dealers across 2 different dealer IDs
        SeedDealer(context, dealerId: Guid.NewGuid(), name: "Alpha");
        SeedDealer(context, dealerId: Guid.NewGuid(), name: "Beta");
        SeedDealer(context, dealerId: Guid.NewGuid(), name: "Gamma", isActive: false);

        var handler = new GetAllDealersQueryHandler(context);
        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_IncludesSuspendedDealers()
    {
        using var context = CreateContext();

        SeedDealer(context, name: "Active");
        SeedDealer(context, name: "Suspended", isActive: false);

        var handler = new GetAllDealersQueryHandler(context);
        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().ContainSingle(d => !d.IsActive);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoDealers()
    {
        using var context = CreateContext();

        var handler = new GetAllDealersQueryHandler(context);
        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ETagIsFormattedWithVPrefix()
    {
        using var context = CreateContext();
        SeedDealer(context, name: "Solo");

        var handler = new GetAllDealersQueryHandler(context);
        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Single().ETag.Should().StartWith("v");
    }

    // spr PR 2.1 (BE-PLATFORM-06/07, D3): a dealer with an Active subscription row exposes the real
    // billing snapshot and resolves to DealerOperationalStatus.Active.
    [Fact]
    public async Task Handle_DealerWithActiveSubscription_ExposesSubscriptionSnapshotAndActiveOperationalStatus()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        SeedDealer(context, dealerId: dealerId, name: "Alpha");

        var periodStart = DateTime.UtcNow;
        var periodEnd = periodStart.AddMonths(1);
        var subscription = DealerSubscription.Create(dealerId, "cus_123", "sub_123", "price_123");
        subscription.Activate("sub_123", periodStart, periodEnd);
        context.DealerSubscriptions.Add(subscription);
        context.SaveChanges();

        var handler = new GetAllDealersQueryHandler(context);
        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dealer = result.Value.Single(d => d.DealerId == dealerId);
        dealer.SubscriptionStatus.Should().Be(SubscriptionStatus.Active);
        dealer.PlanCode.Should().Be(DealerSubscription.DefaultPlanCode);
        dealer.NextChargeAt.Should().Be(periodEnd);
        dealer.OperationalStatus.Should().Be(DealerOperationalStatus.Active);
    }

    // spr PR 2.1: a legacy dealer with no DealerSubscription row at all must never throw, and must
    // surface as BillingPending (not Active) so the super-admin sees it needs attention.
    [Fact]
    public async Task Handle_DealerWithoutSubscriptionRow_ReturnsNullsAndBillingPending_WithoutThrowing()
    {
        using var context = CreateContext();
        SeedDealer(context, name: "NoSubscriptionRow");

        var handler = new GetAllDealersQueryHandler(context);

        Func<Task> act = async () => await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);
        await act.Should().NotThrowAsync();

        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);
        var dealer = result.Value.Single();
        dealer.SubscriptionStatus.Should().BeNull();
        dealer.PlanCode.Should().BeNull();
        dealer.TrialEndsAt.Should().BeNull();
        dealer.NextChargeAt.Should().BeNull();
        dealer.OperationalStatus.Should().Be(DealerOperationalStatus.BillingPending);
    }

    // spr PR 2.1: PastDue is a non-paying standing — it must never resolve to Active, regardless of
    // the (separate, not-yet-wired) grace period decided in DealerOperationalStatusResolver.DecideAccess.
    [Fact]
    public async Task Handle_DealerWithPastDueSubscription_IsNotReportedAsActive()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        SeedDealer(context, dealerId: dealerId, name: "PastDueDealer");

        var subscription = DealerSubscription.Create(dealerId, "cus_456", "sub_456", "price_123");
        subscription.Activate("sub_456", DateTime.UtcNow.AddMonths(-2), DateTime.UtcNow.AddMonths(-1));
        subscription.MarkPastDue();
        context.DealerSubscriptions.Add(subscription);
        context.SaveChanges();

        var handler = new GetAllDealersQueryHandler(context);
        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);

        var dealer = result.Value.Single(d => d.DealerId == dealerId);
        dealer.SubscriptionStatus.Should().Be(SubscriptionStatus.PastDue);
        dealer.OperationalStatus.Should().Be(DealerOperationalStatus.PastDue);
        dealer.OperationalStatus.Should().NotBe(DealerOperationalStatus.Active);
    }

    // spr PR 2.1: a dealer Suspended by the provider (e.g. Stripe cancelled after repeated failures)
    // must not be reported as Active even though DealerSettings.IsActive is still true.
    [Fact]
    public async Task Handle_DealerWithSuspendedSubscription_IsNotReportedAsActive()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        SeedDealer(context, dealerId: dealerId, name: "SuspendedDealer");

        var subscription = DealerSubscription.Create(dealerId, "cus_789", "sub_789", "price_123");
        subscription.Activate("sub_789", DateTime.UtcNow.AddMonths(-3), DateTime.UtcNow.AddMonths(-2));
        subscription.MarkPastDue();
        subscription.Suspend();
        context.DealerSubscriptions.Add(subscription);
        context.SaveChanges();

        var handler = new GetAllDealersQueryHandler(context);
        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);

        var dealer = result.Value.Single(d => d.DealerId == dealerId);
        dealer.SubscriptionStatus.Should().Be(SubscriptionStatus.Suspended);
        dealer.OperationalStatus.Should().Be(DealerOperationalStatus.Suspended);
        dealer.OperationalStatus.Should().NotBe(DealerOperationalStatus.Active);
    }

    // spr PR 2.1: manual suspension (IsActive == false) outranks billing standing even when the
    // subscription itself is Active — SuspendedManual, never Active or BillingPending.
    [Fact]
    public async Task Handle_ManuallySuspendedDealerWithActiveSubscription_ReportsSuspendedManual()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        SeedDealer(context, dealerId: dealerId, name: "ManuallySuspended", isActive: false);

        var subscription = DealerSubscription.Create(dealerId, "cus_999", "sub_999", "price_123");
        subscription.Activate("sub_999", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        context.DealerSubscriptions.Add(subscription);
        context.SaveChanges();

        var handler = new GetAllDealersQueryHandler(context);
        var result = await handler.Handle(new GetAllDealersQuery(), CancellationToken.None);

        var dealer = result.Value.Single(d => d.DealerId == dealerId);
        dealer.OperationalStatus.Should().Be(DealerOperationalStatus.SuspendedManual);
    }
}
