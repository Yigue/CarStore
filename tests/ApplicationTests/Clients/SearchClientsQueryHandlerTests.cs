using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Clients.Search;
using Application.Clients.GetAll;
using Domain.Clients;
using Domain.Clients.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Clients;

public class SearchClientsQueryHandlerTests
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

        var clients = new[]
        {
            new Client(dealerId, "Juan", "Perez", "12345678", "juan@test.com", "111", "Addr1", now),
            new Client(dealerId, "Maria", "Garcia", "23456789", "maria@test.com", "222", "Addr2", now),
            new Client(dealerId, "Pedro", "Juanes", "34567890", "pedro@test.com", "333", "Addr3", now),
            new Client(dealerId, "Ana", "Lopez", "45678901", "ana@test.com", "444", "Addr4", now),
            new Client(dealerId, "Carlos", "Martinez", "56789012", "carlos@test.com", "555", "Addr5", now),
        };

        context.Clients.AddRange(clients);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ReturnsMatchingClients()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new SearchClientsQueryHandler(context);
        var query = new SearchClientsQuery { SearchTerm = "Juan" };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var clients = result.Value.ToList();
        clients.Should().HaveCount(2);
        clients.Should().Contain(c => c.FirstName == "Juan");
        clients.Should().Contain(c => c.FirstName == "Pedro"); // LastName contains "Juan"
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoMatch()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new SearchClientsQueryHandler(context);
        var query = new SearchClientsQuery { SearchTerm = "xyz123" };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LimitsTo50Results()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Add 60 clients with same first name to exceed limit
        for (int i = 0; i < 60; i++)
        {
            context.Clients.Add(new Client(dealerId, $"TestUser{i}", "Testing", $"{i}2345678", $"user{i}@test.com", "111", $"Addr{i}", now));
        }
        await context.SaveChangesAsync();

        var handler = new SearchClientsQueryHandler(context);
        var query = new SearchClientsQuery { SearchTerm = "Test" };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count().Should().BeLessOrEqualTo(50);
    }

    [Fact]
    public async Task Handle_IsCaseInsensitive()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new SearchClientsQueryHandler(context);
        var query = new SearchClientsQuery { SearchTerm = "MARIA" };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var clients = result.Value.ToList();
        clients.Should().HaveCount(1);
        clients[0].FirstName.Should().Be("Maria");
    }

    [Fact]
    public async Task Handle_SearchesByEmail()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new SearchClientsQueryHandler(context);
        var query = new SearchClientsQuery { SearchTerm = "carlos@test.com" };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var clients = result.Value.ToList();
        clients.Should().HaveCount(1);
        clients[0].FirstName.Should().Be("Carlos");
    }
}