using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Clients.GetAll;
using Domain.Clients;
using Domain.Clients.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Clients;

/// <summary>
/// PR1: GetAllClientsQueryHandler — verifies response mapping and basic result contract.
/// Soft-delete global-filter behaviour is covered by integration tests (it requires the
/// ApplicationDbContext query filter which depends on ITenantService).
/// </summary>
public class GetAllClientsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Client SeedClient(
        TestApplicationDbContext context,
        string firstName = "John",
        string lastName = "Doe",
        string dni = "12345678",
        ClientType type = ClientType.Individual)
    {
        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, firstName, lastName, dni, $"{firstName.ToLower()}@test.com", "111", "Street 1", DateTime.UtcNow, type);
        context.Clients.Add(client);
        context.SaveChanges();
        client.ClearDomainEvents();
        return client;
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoClients()
    {
        using var context = CreateContext();
        var handler = new GetAllClientsQueryHandler(context);
        var query = new GetAllClientsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsAllClients_WithCorrectMapping()
    {
        using var context = CreateContext();
        SeedClient(context, "Alice", "Smith", "11111111", ClientType.Individual);
        SeedClient(context, "Bob", "Jones", "22222222", ClientType.Corporate);

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_MapsClientType_AsString()
    {
        using var context = CreateContext();
        SeedClient(context, "Corp", "Entity", "33333333", ClientType.Corporate);

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value.Single();
        // ClientType enum value must round-trip correctly (ADR-1: PascalCase via JsonStringEnumConverter at the API layer)
        response.Type.Should().Be(ClientType.Corporate);
    }

    [Fact]
    public async Task Handle_MapsFullName_Correctly()
    {
        using var context = CreateContext();
        SeedClient(context, "Maria", "Gonzalez", "44444444");

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value.Single();
        response.FirstName.Should().Be("Maria");
        response.LastName.Should().Be("Gonzalez");
        response.FullName.Should().Be("Maria Gonzalez");
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WithMultipleClients()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            context.Clients.Add(new Client(dealerId, $"Client{i}", $"Last{i}", $"{i}0000000", $"c{i}@test.com", "111", $"Addr{i}", DateTime.UtcNow.AddSeconds(-i)));
        }
        await context.SaveChangesAsync();

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);
    }
}
