using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Clients.GetStats;
using Domain.Clients;
using Domain.Clients.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Clients;

public class GetClientStatsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task SeedTestDataAsync(TestApplicationDbContext context)
    {
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Create 5 active clients - 3 recent
        context.Clients.Add(new Client(dealerId, "Juan", "Perez", "123", "juan@test.com", "111", "Addr1", now.AddDays(-5)));
        context.Clients.Add(new Client(dealerId, "Maria", "Garcia", "234", "maria@test.com", "222", "Addr2", now.AddDays(-10)));
        context.Clients.Add(new Client(dealerId, "Pedro", "Martinez", "345", "pedro@test.com", "333", "Addr3", now.AddDays(-20)));
        context.Clients.Add(new Client(dealerId, "Ana", "Lopez", "456", "ana@test.com", "444", "Addr4", now.AddDays(-40)));
        context.Clients.Add(new Client(dealerId, "Inactive", "Client", "567", "inactive@test.com", "555", "Addr5", now.AddDays(-60)));

        // Deactivate one client
        var inactiveClient = new Client(dealerId, "Old", "Client", "789", "old@test.com", "666", "Addr6", now.AddDays(-90));
        inactiveClient.Deactivate();
        context.Clients.Add(inactiveClient);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCorrectTotalCount()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetClientStatsQueryHandler(context);
        var query = new GetClientStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(6);
    }

    [Fact]
    public async Task Handle_ComputesBySourceCorrectly()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetClientStatsQueryHandler(context);
        var query = new GetClientStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Groups by Status - Active and Inactive
        result.Value.BySource.Should().ContainKey("Active");
        result.Value.BySource.Should().ContainKey("Inactive");
        result.Value.BySource["Active"].Should().Be(5);
        result.Value.BySource["Inactive"].Should().Be(1);
    }

    [Fact]
    public async Task Handle_RecentCount_FiltersLast30Days()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetClientStatsQueryHandler(context);
        var query = new GetClientStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 3 clients created in last 30 days
        result.Value.RecentCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ActiveCount_ReturnsOnlyActiveClients()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetClientStatsQueryHandler(context);
        var query = new GetClientStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ActiveCount.Should().Be(5); // 5 Active, 1 Inactive
    }

    [Fact]
    public async Task Handle_ReturnsZeros_WhenNoClients()
    {
        using var context = CreateContext();
        var handler = new GetClientStatsQueryHandler(context);
        var query = new GetClientStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.RecentCount.Should().Be(0);
        result.Value.ActiveCount.Should().Be(0);
    }
}