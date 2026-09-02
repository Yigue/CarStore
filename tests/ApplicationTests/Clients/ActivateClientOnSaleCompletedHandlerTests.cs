using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Clients.Events;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests.Clients;

public class ActivateClientOnSaleCompletedHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Client SeedClient(TestApplicationDbContext context, Action<Client>? configure = null)
    {
        var client = new Client(
            Guid.NewGuid(),
            "Juan",
            "Perez",
            "20123456",
            "juan@test.com",
            "123456789",
            "Calle Falsa 123",
            DateTime.UtcNow);

        configure?.Invoke(client);

        context.Clients.Add(client);
        context.SaveChanges();
        return client;
    }

    [Fact]
    public async Task Handle_ProspectClient_ActivatesClient()
    {
        using var context = CreateContext();
        var client = SeedClient(context, c => c.SetProspect());
        client.Status.Should().Be(ClientStatus.Prospect);

        var handler = new ActivateClientOnSaleCompletedHandler(context);
        var @event = new SaleCompletedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            client.Id,
            new Money(15000m, "USD"),
            PaymentMethod.BankTransfer);

        await handler.Handle(@event, CancellationToken.None);

        var updatedClient = await context.Clients.FindAsync(client.Id);
        updatedClient!.Status.Should().Be(ClientStatus.Active);
    }

    [Fact]
    public async Task Handle_ClientNotFound_IsNoOp()
    {
        using var context = CreateContext();
        var handler = new ActivateClientOnSaleCompletedHandler(context);
        var @event = new SaleCompletedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Money(15000m, "USD"),
            PaymentMethod.BankTransfer);

        var act = () => handler.Handle(@event, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_AlreadyActiveClient_KeepsActiveStatus()
    {
        using var context = CreateContext();
        var client = SeedClient(context);
        client.Status.Should().Be(ClientStatus.Active);

        var handler = new ActivateClientOnSaleCompletedHandler(context);
        var @event = new SaleCompletedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            client.Id,
            new Money(15000m, "USD"),
            PaymentMethod.BankTransfer);

        await handler.Handle(@event, CancellationToken.None);

        var updatedClient = await context.Clients.FindAsync(client.Id);
        updatedClient!.Status.Should().Be(ClientStatus.Active);
    }
}
