using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.UpdateStatus;
using Domain.Leads;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

public class UpdateLeadStatusFromSaleHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_ForceLeadToGanado_WhenSaleCreatedForLead()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Bob Jones", "bob@test.com", "9876543", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var handler = new UpdateLeadStatusFromSaleHandler(context);
        var notification = new SaleCreatedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Money(10000m), lead.Id);

        await handler.Handle(notification, CancellationToken.None);

        var updated = await context.Leads.FindAsync(lead.Id);
        updated!.Status.Should().Be(LeadStatus.Ganado);
    }

    [Fact]
    public async Task Handle_Should_DoNothing_WhenSaleHasNoLeadId()
    {
        using var context = CreateContext();
        var handler = new UpdateLeadStatusFromSaleHandler(context);
        var notification = new SaleCreatedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Money(10000m), null);

        // Should not throw despite no lead lookup being possible.
        await handler.Handle(notification, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Should_DoNothing_WhenLeadNotFound()
    {
        using var context = CreateContext();
        var handler = new UpdateLeadStatusFromSaleHandler(context);
        var notification = new SaleCreatedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Money(10000m), Guid.NewGuid());

        // Should not throw when the lead cannot be resolved.
        await handler.Handle(notification, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Should_BeIdempotent_WhenLeadAlreadyGanado()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Carol White", "carol@test.com", "1112223", LeadSource.Web, DateTime.UtcNow);
        lead.ForceStatus(LeadStatus.Ganado);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var handler = new UpdateLeadStatusFromSaleHandler(context);
        var notification = new SaleCreatedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Money(10000m), lead.Id);

        await handler.Handle(notification, CancellationToken.None);

        var updated = await context.Leads.FindAsync(lead.Id);
        updated!.Status.Should().Be(LeadStatus.Ganado);
    }
}
