using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.CreateClient;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Leads.Events;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

/// <summary>
/// REQ-CRM-PROSPECT-001: CreateClientFromLeadOnNegociacionHandler auto-creates a Prospect
/// Client when a Lead reaches Negociación, and marks that Client Lost when the Lead reaches
/// Perdido (ADR-3). Mirrors AdvanceLeadOnDemoAppointmentCreatedHandler's outbox-decoupled
/// event-subscription pattern (see design.md §1, §5).
/// </summary>
public class CreateClientFromLeadOnNegociacionHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Lead CreateLead(Guid dealerId, string name = "Carlos Perez", string email = "carlos@test.com") =>
        Lead.Create(dealerId, name, email, "1112223", LeadSource.Web, DateTime.UtcNow);

    [Fact]
    public async Task Negociacion_NoExistingClient_CreatesProspectAndMarksConverted()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        var notification = new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion);

        await handler.Handle(notification, CancellationToken.None);

        var clients = await context.Clients.ToListAsync();
        clients.Should().ContainSingle();
        clients[0].Status.Should().Be(ClientStatus.Prospect);
        clients[0].OriginLeadId.Should().Be(lead.Id);

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        updatedLead!.ConvertedClientId.Should().Be(clients[0].Id);
    }

    [Fact]
    public async Task Negociacion_ConvertedClientIdAlreadySet_IsIdempotent()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        var notification = new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion);

        await handler.Handle(notification, CancellationToken.None);
        await handler.Handle(notification, CancellationToken.None); // retry, e.g. outbox re-tick

        var clientCount = await context.Clients.CountAsync();
        clientCount.Should().Be(1, "re-running the handler for an already-converted lead must not create a duplicate");
    }

    [Fact]
    public async Task Negociacion_EmailMatchesExistingClient_ReusesIt()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId, email: "ana@test.com");
        var existingClient = new Client(dealerId, "Ana", "Lopez", "20444555", "ana@test.com", "4445556", "Some address", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        var notification = new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion);

        await handler.Handle(notification, CancellationToken.None);

        var clientCount = await context.Clients.CountAsync();
        clientCount.Should().Be(1, "an existing client with a matching email must be reused, not duplicated");

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        updatedLead!.ConvertedClientId.Should().Be(existingClient.Id);
    }

    [Fact]
    public async Task Negociacion_ExistingQuoteLinkedToLead_IsReassignedToNewClient()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId);
        context.Leads.Add(lead);
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "NEG001", "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var quote = new Quote(dealerId, car, null, lead, 100_000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(5), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        var notification = new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion);

        await handler.Handle(notification, CancellationToken.None);

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        var updatedQuote = await context.Quotes.FindAsync(quote.Id);

        updatedQuote!.ClientId.Should().Be(updatedLead!.ConvertedClientId);
        updatedQuote.LeadId.Should().Be(lead.Id,
            "attaching the client keeps the enquiry the deal came from — the quote belongs to the person, not to one of the two records");
    }

    [Fact]
    public async Task Perdido_WithConvertedClient_MarksLost()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        await handler.Handle(new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion), CancellationToken.None);

        await handler.Handle(new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Negociacion, LeadStatus.Perdido), CancellationToken.None);

        var client = await context.Clients.SingleAsync();
        client.Status.Should().Be(ClientStatus.Lost);
    }

    [Fact]
    public async Task NonTargetStatus_NoOp()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        await handler.Handle(new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Demostracion, LeadStatus.Ganado), CancellationToken.None);

        var clientCount = await context.Clients.CountAsync();
        clientCount.Should().Be(0, "Ganado is handled by the existing conversion handlers, not this one");

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        updatedLead!.ConvertedClientId.Should().BeNull();
    }
}
