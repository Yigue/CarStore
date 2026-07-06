using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Platform.Dealers.GetAllDealers;
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
}
