using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Platform.Metrics.GetPlatformMetrics;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Platform.Metrics;

public class GetPlatformMetricsQueryHandlerTests
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

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsZeroCounts()
    {
        using var context = CreateContext();
        var handler = new GetPlatformMetricsQueryHandler(context);

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

        var handler = new GetPlatformMetricsQueryHandler(context);
        var result = await handler.Handle(new GetPlatformMetricsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalDealers.Should().Be(3);
        result.Value.ActiveDealers.Should().Be(2);
        result.Value.SuspendedDealers.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsMrrStub()
    {
        using var context = CreateContext();
        var handler = new GetPlatformMetricsQueryHandler(context);

        var result = await handler.Handle(new GetPlatformMetricsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Mrr.IsStub.Should().BeTrue();
        result.Value.Mrr.StubReason.Should().Be("Requires saas-subscription-payments");
    }
}
