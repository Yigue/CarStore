using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Tenancy;
using Domain.Billing.Events;
using Domain.DealerSettings;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Application.UnitTests.Tenancy;

public class StartTenantRetentionHandlerTests
{
    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IConfiguration Config(string? days = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(days is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["Retention:PurgeAfterDays"] = days })
            .Build();

    private static async Task<(TestApplicationDbContext Ctx, Domain.DealerSettings.DealerSettings Dealer)> SeedAsync()
    {
        var ctx = CreateContext();
        var dealer = new Domain.DealerSettings.DealerSettings(Guid.NewGuid(), "Dealer", "d@d.com");
        ctx.DealerSettings.Add(dealer);
        await ctx.SaveChangesAsync();
        return (ctx, dealer);
    }

    [Fact]
    public async Task Cancelled_SchedulesPurgeWithDefaultNinetyDays()
    {
        var (ctx, dealer) = await SeedAsync();
        var handler = new StartTenantRetentionHandler(ctx, Config());
        var cancelledAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        await handler.Handle(new SubscriptionCancelledDomainEvent(Guid.NewGuid(), dealer.DealerId, cancelledAt), CancellationToken.None);

        var stored = await ctx.DealerSettings.IgnoreQueryFilters().SingleAsync();
        stored.LifecycleStatus.Should().Be(TenantLifecycleStatus.CancelledGrace);
        stored.CancelledAt.Should().Be(cancelledAt);
        stored.PurgeScheduledAt.Should().Be(cancelledAt.AddDays(90));
    }

    [Fact]
    public async Task Cancelled_HonoursConfiguredRetentionDays()
    {
        var (ctx, dealer) = await SeedAsync();
        var handler = new StartTenantRetentionHandler(ctx, Config("60"));
        var cancelledAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        await handler.Handle(new SubscriptionCancelledDomainEvent(Guid.NewGuid(), dealer.DealerId, cancelledAt), CancellationToken.None);

        (await ctx.DealerSettings.IgnoreQueryFilters().SingleAsync()).PurgeScheduledAt.Should().Be(cancelledAt.AddDays(60));
    }

    [Fact]
    public async Task Activated_AfterCancellation_ClearsPurgeSchedule()
    {
        var (ctx, dealer) = await SeedAsync();
        var handler = new StartTenantRetentionHandler(ctx, Config());
        await handler.Handle(new SubscriptionCancelledDomainEvent(Guid.NewGuid(), dealer.DealerId, DateTime.UtcNow.AddDays(-10)), CancellationToken.None);

        await handler.Handle(new SubscriptionActivatedDomainEvent(Guid.NewGuid(), dealer.DealerId, "sub_1"), CancellationToken.None);

        var stored = await ctx.DealerSettings.IgnoreQueryFilters().SingleAsync();
        stored.LifecycleStatus.Should().Be(TenantLifecycleStatus.Active);
        stored.PurgeScheduledAt.Should().BeNull();
        stored.CancelledAt.Should().BeNull();
    }

    [Fact]
    public async Task Cancelled_ForUnknownDealer_DoesNothing()
    {
        var ctx = CreateContext();
        var handler = new StartTenantRetentionHandler(ctx, Config());

        var act = () => handler.Handle(new SubscriptionCancelledDomainEvent(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
