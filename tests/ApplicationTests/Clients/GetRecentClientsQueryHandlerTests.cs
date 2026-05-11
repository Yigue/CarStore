using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Clients.GetRecent;
using Application.Clients.GetAll;
using Domain.Clients;
using Domain.Clients.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Clients;

public class GetRecentClientsQueryHandlerTests
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

        // Create clients with different creation dates
        var dates = new[]
        {
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(-2),
            DateTime.UtcNow.AddDays(-3),
            DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow.AddDays(-20),
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(-40),
        };

        for (int i = 0; i < dates.Length; i++)
        {
            context.Clients.Add(new Client(dealerId, $"Client{i}", $"Lastname{i}", $"{i}2345678", $"client{i}@test.com", "111", $"Addr{i}", dates[i]));
        }
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOrderedByCreatedAtDesc()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetRecentClientsQueryHandler(context);
        var query = new GetRecentClientsQuery { Limit = 10 };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var clients = result.Value.ToList();
        clients.Should().HaveCount(8);

        // First client should be most recent
        clients[0].FirstName.Should().Be("Client0");
        // Last client should be oldest
        clients[7].FirstName.Should().Be("Client7");
    }

    [Fact]
    public async Task Handle_RespectsLimit()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetRecentClientsQueryHandler(context);
        var query = new GetRecentClientsQuery { Limit = 3 };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_CapsAt100Max()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Add 150 clients
        for (int i = 0; i < 150; i++)
        {
            context.Clients.Add(new Client(dealerId, $"Client{i}", $"Last{i}", $"{i}2345678", $"c{i}@test.com", "111", $"Addr{i}", now.AddMinutes(-i)));
        }
        await context.SaveChangesAsync();

        var handler = new GetRecentClientsQueryHandler(context);
        var query = new GetRecentClientsQuery { Limit = 200 }; // Request more than max

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(100); // Should cap at 100
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoClients()
    {
        using var context = CreateContext();
        var handler = new GetRecentClientsQueryHandler(context);
        var query = new GetRecentClientsQuery { Limit = 10 };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UsesDefaultLimitOf10()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetRecentClientsQueryHandler(context);
        var query = new GetRecentClientsQuery(); // No limit specified, defaults to 10

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(8); // Only 8 clients exist
    }
}