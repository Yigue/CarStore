using Application.Clients.GetActivity;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Shared;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.UnitTests.Clients;

/// <summary>
/// PR2 RED→GREEN: GetActivityQueryHandler projects OutboxMessages
/// filtered by AggregateId + AggregateType='Client'.
/// </summary>
public class GetActivityQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static void SeedClient(TestApplicationDbContext context, Guid clientId, Guid dealerId)
    {
        var client = new Client(dealerId, "Test", "Client", "12345678", "test@example.com", "555", "Av. Test 1", DateTime.UtcNow);
        
        var idProp = typeof(Entity).GetProperty(nameof(Entity.Id));
        idProp?.SetValue(client, clientId);
        
        context.Clients.Add(client);
        context.SaveChanges();
    }

    private static OutboxMessage MakeMessage(Guid clientId, Guid dealerId, string eventType, DateTime occurredAt)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = eventType,
            Content = "{}",
            OccurredOnUtc = occurredAt,
            AggregateId = clientId,
            AggregateType = "Client",
            DealerId = dealerId
        };

    [Fact]
    public async Task Handle_Returns_Empty_When_No_Events_For_Client()
    {
        using var context = CreateContext();
        var clientId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        SeedClient(context, clientId, dealerId);
        
        var handler = new GetActivityQueryHandler(context);

        var result = await handler.Handle(
            new GetActivityQuery(clientId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Returns_Only_Events_For_Requested_Client()
    {
        using var context = CreateContext();
        var clientId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        SeedClient(context, clientId, dealerId);
        
        var otherId = Guid.NewGuid();

        context.OutboxMessages.AddRange(
            MakeMessage(clientId, dealerId, "ClientNotesUpdatedDomainEvent", DateTime.UtcNow),
            MakeMessage(otherId, dealerId, "ClientNotesUpdatedDomainEvent", DateTime.UtcNow)
        );
        await context.SaveChangesAsync();

        var handler = new GetActivityQueryHandler(context);
        var result = await handler.Handle(new GetActivityQuery(clientId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle(e => e.EventType == "ClientNotesUpdatedDomainEvent");
    }

    [Fact]
    public async Task Handle_Does_Not_Return_NonClient_AggregateType_Events()
    {
        using var context = CreateContext();
        var clientId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        SeedClient(context, clientId, dealerId);
        
        var now = DateTime.UtcNow;

        // Message with same AggregateId but different AggregateType (e.g. Lead)
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "LeadStatusChangedDomainEvent",
            Content = "{}",
            OccurredOnUtc = now,
            AggregateId = clientId,
            AggregateType = "Lead",
            DealerId = dealerId
        });
        await context.SaveChangesAsync();

        var handler = new GetActivityQueryHandler(context);
        var result = await handler.Handle(new GetActivityQuery(clientId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Returns_Events_OrderedBy_OccurredAtUtc_Descending()
    {
        using var context = CreateContext();
        var clientId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        SeedClient(context, clientId, dealerId);
        
        var base_ = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        context.OutboxMessages.AddRange(
            MakeMessage(clientId, dealerId, "EventA", base_.AddHours(1)),
            MakeMessage(clientId, dealerId, "EventB", base_.AddHours(3)),
            MakeMessage(clientId, dealerId, "EventC", base_.AddHours(2))
        );
        await context.SaveChangesAsync();

        var handler = new GetActivityQueryHandler(context);
        var result = await handler.Handle(new GetActivityQuery(clientId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(e => e.EventType)
            .Should().ContainInOrder("EventB", "EventC", "EventA");
    }

    [Fact]
    public async Task Handle_Paginates_Results_Correctly()
    {
        using var context = CreateContext();
        var clientId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        SeedClient(context, clientId, dealerId);
        
        var base_ = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 10; i++)
        {
            context.OutboxMessages.Add(
                MakeMessage(clientId, dealerId, $"Event{i}", base_.AddMinutes(i)));
        }
        await context.SaveChangesAsync();

        var handler = new GetActivityQueryHandler(context);
        var result = await handler.Handle(new GetActivityQuery(clientId, Page: 2, PageSize: 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(10);
        result.Value.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_Includes_Sale_Events_For_This_Clients_Sales()
    {
        // Sale domain events land in the outbox keyed by SaleId (AggregateType="Sale"),
        // not by ClientId — the handler must fold them into the client's timeline by
        // resolving the client's own sale ids first.
        using var context = CreateContext();
        var clientId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        SeedClient(context, clientId, dealerId);

        var sale = new Sale(
            dealerId,
            Guid.NewGuid(),
            clientId,
            10000m,
            PaymentMethod.Cash,
            "CN-1",
            "comment",
            DateTime.UtcNow);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "SaleCreatedDomainEvent",
            Content = "{}",
            OccurredOnUtc = DateTime.UtcNow,
            AggregateId = sale.Id,
            AggregateType = "Sale",
            DealerId = dealerId
        });
        await context.SaveChangesAsync();

        var handler = new GetActivityQueryHandler(context);
        var result = await handler.Handle(new GetActivityQuery(clientId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle(e => e.EventType == "SaleCreatedDomainEvent");
    }

    [Fact]
    public async Task Handle_Does_Not_Include_Sale_Events_For_Other_Clients_Sales()
    {
        using var context = CreateContext();
        var clientId = Guid.NewGuid();
        var dealerId = Guid.NewGuid();
        SeedClient(context, clientId, dealerId);

        var otherClientId = Guid.NewGuid();
        var otherSale = new Sale(
            dealerId,
            Guid.NewGuid(),
            otherClientId,
            10000m,
            PaymentMethod.Cash,
            "CN-2",
            "comment",
            DateTime.UtcNow);
        context.Sales.Add(otherSale);
        await context.SaveChangesAsync();

        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "SaleCreatedDomainEvent",
            Content = "{}",
            OccurredOnUtc = DateTime.UtcNow,
            AggregateId = otherSale.Id,
            AggregateType = "Sale",
            DealerId = dealerId
        });
        await context.SaveChangesAsync();

        var handler = new GetActivityQueryHandler(context);
        var result = await handler.Handle(new GetActivityQuery(clientId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}
