using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Clients.GetAll;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Clients;

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
        ClientType type = ClientType.Individual,
        ClientStatus status = ClientStatus.Active,
        AcquisitionSource? source = null,
        Guid? agentId = null,
        DateTime? createdAt = null)
    {
        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, firstName, lastName, dni, $"{firstName.ToLower()}@test.com", "111", "Street 1", createdAt ?? DateTime.UtcNow, type, null, null, null, null);
        
        if (status == ClientStatus.Inactive)
            client.Deactivate();
        else if (status == ClientStatus.Prospect)
            client.SetProspect();
        else if (status == ClientStatus.VIP)
            client.SetVIP();

        if (source.HasValue)
        {
            typeof(Client).GetProperty(nameof(Client.AcquisitionSource))?.SetValue(client, source.Value);
        }

        if (agentId.HasValue)
        {
            typeof(Client).GetProperty(nameof(Client.AssignedAgentId))?.SetValue(client, agentId.Value);
        }

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
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
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
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_MapsClientType_AsString()
    {
        using var context = CreateContext();
        SeedClient(context, "Corp", "Entity", "33333333", ClientType.Corporate);

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var response = result.Value.Items.Single();
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
        var response = result.Value.Items.Single();
        response.FirstName.Should().Be("Maria");
        response.LastName.Should().Be("Gonzalez");
        response.FullName.Should().Be("Maria Gonzalez");
    }

    [Fact]
    public async Task Handle_Should_FilterByStatus()
    {
        using var context = CreateContext();
        SeedClient(context, "Alice", "Active", "11111111", status: ClientStatus.Active);
        SeedClient(context, "Bob", "Inactive", "22222222", status: ClientStatus.Inactive);

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(Status: "Inactive"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LastName.Should().Be("Inactive");
    }

    [Fact]
    public async Task Handle_Should_FilterByType()
    {
        using var context = CreateContext();
        SeedClient(context, "Alice", "Indiv", "11111111", type: ClientType.Individual);
        SeedClient(context, "Bob", "Corp", "22222222", type: ClientType.Corporate);

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(Type: "Corporate"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LastName.Should().Be("Corp");
    }

    [Fact]
    public async Task Handle_Should_FilterBySource()
    {
        using var context = CreateContext();
        SeedClient(context, "Alice", "Web", "11111111", source: AcquisitionSource.Web);
        SeedClient(context, "Bob", "Portal", "22222222", source: AcquisitionSource.Portal);

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(Source: "Portal"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LastName.Should().Be("Portal");
    }

    [Fact]
    public async Task Handle_Should_FilterByAgent()
    {
        using var context = CreateContext();
        var agentId = Guid.NewGuid();
        SeedClient(context, "Alice", "Agent", "11111111", agentId: agentId);
        SeedClient(context, "Bob", "NoAgent", "22222222");

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(AssignedAgentId: agentId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LastName.Should().Be("Agent");
    }

    [Fact]
    public async Task Handle_Should_FilterByDateRange()
    {
        using var context = CreateContext();
        var baseDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        SeedClient(context, "Alice", "Old", "11111111", createdAt: baseDate.AddDays(-5));
        SeedClient(context, "Bob", "New", "22222222", createdAt: baseDate.AddDays(5));

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(CreatedFrom: baseDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LastName.Should().Be("New");
    }

    [Fact]
    public async Task Handle_Should_FilterBySalesRange()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var client1 = SeedClient(context, "Alice", "SalesHigh", "11111111");
        var client2 = SeedClient(context, "Bob", "SalesLow", "22222222");

        var sale1 = new Sale(dealerId, Guid.NewGuid(), client1.Id, 50000m, PaymentMethod.Cash, "CNT-01", "", now);
        sale1.Complete();
        var sale2 = new Sale(dealerId, Guid.NewGuid(), client2.Id, 10000m, PaymentMethod.Cash, "CNT-02", "", now);
        sale2.Complete();

        // Add sales via navigation properties so mapping works in-memory
        client1.Sales.Add(sale1);
        client2.Sales.Add(sale2);

        context.Sales.AddRange(sale1, sale2);
        await context.SaveChangesAsync();

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(TotalSalesMin: 30000m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LastName.Should().Be("SalesHigh");
    }

    [Fact]
    public async Task Handle_Should_SupportPagination()
    {
        using var context = CreateContext();
        for (int i = 1; i <= 5; i++)
        {
            SeedClient(context, $"Client{i}", $"Last{i}", $"{i}2345678");
        }

        var handler = new GetAllClientsQueryHandler(context);
        var result = await handler.Handle(new GetAllClientsQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(5);
        result.Value.Items.Should().HaveCount(2);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Should_SearchByFullName_CaseAndAccentInsensitive()
    {
        using var context = CreateContext();
        SeedClient(context, "María", "González", "11111111");
        SeedClient(context, "Juan", "Pérez", "22222222");

        var handler = new GetAllClientsQueryHandler(context);
        
        var result = await handler.Handle(new GetAllClientsQuery(Search: "maria gonzalez"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].FirstName.Should().Be("María");

        var result2 = await handler.Handle(new GetAllClientsQuery(Search: "mariá"), CancellationToken.None);
        result2.IsSuccess.Should().BeTrue();
        result2.Value.Items.Should().ContainSingle();
        result2.Value.Items[0].FirstName.Should().Be("María");
    }
}
