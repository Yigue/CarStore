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
using Domain.Sales;
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

    // ── LEAD-03 / VEN-01: a recycled client has to come back usable ──────────────────────────
    //
    // CreateSaleCommandHandler refuses a Lost or Inactive client (ClientErrors.Inactive). When
    // the conversion reuses one of those records instead of creating a fresh Prospect, the board
    // reaches Ganado, the sale form opens, and the API answers 400 about a client the operator
    // never touched.

    [Fact]
    public async Task Negociacion_ReusesALostClient_RevivesItAsProspect()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId, email: "vuelve@test.com");
        var existingClient = new Client(dealerId, "Ana", "Lopez", "20444555", "vuelve@test.com", "4445556", "Addr", DateTime.UtcNow);
        existingClient.MarkAsLost();
        context.Leads.Add(lead);
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        await handler.Handle(
            new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion),
            CancellationToken.None);

        var client = await context.Clients.SingleAsync();
        client.Status.Should().Be(
            ClientStatus.Prospect,
            "negotiating again is what revives the record — otherwise the sale that follows is rejected as Clients.Inactive");
    }

    [Fact]
    public async Task Negociacion_ReusesAnInactiveClient_RevivesItAsProspect()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId, email: "dormido@test.com");
        var existingClient = new Client(dealerId, "Ana", "Lopez", "20444555", "dormido@test.com", "4445556", "Addr", DateTime.UtcNow);
        existingClient.Deactivate();
        context.Leads.Add(lead);
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        await handler.Handle(
            new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion),
            CancellationToken.None);

        var client = await context.Clients.SingleAsync();
        client.Status.Should().Be(ClientStatus.Prospect);
    }

    // Someone who already bought here is a customer, not a prospect: reviving them must not
    // rewrite that history.
    [Fact]
    public async Task Negociacion_ReusesALostClientWithACompletedSale_RevivesItAsActive()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId, email: "repite@test.com");
        var existingClient = new Client(dealerId, "Ana", "Lopez", "20444555", "repite@test.com", "4445556", "Addr", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(existingClient);

        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "REV001", "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var sale = new Sale(dealerId, car.Id, existingClient.Id, 15000m, Domain.Financial.Attributes.PaymentMethod.Cash, "C-1", "", DateTime.UtcNow);
        sale.Complete();
        context.Sales.Add(sale);
        existingClient.MarkAsLost();
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        await handler.Handle(
            new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion),
            CancellationToken.None);

        var client = await context.Clients.SingleAsync();
        client.Status.Should().Be(ClientStatus.Active, "a returning buyer is a customer, not a prospect");
    }

    // A client who is already trading is left exactly as they are.
    [Fact]
    public async Task Negociacion_ReusesAnActiveClient_LeavesItsStatusAlone()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId, email: "activo@test.com");
        var existingClient = new Client(dealerId, "Ana", "Lopez", "20444555", "activo@test.com", "4445556", "Addr", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        await handler.Handle(
            new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion),
            CancellationToken.None);

        var client = await context.Clients.SingleAsync();
        client.Status.Should().Be(ClientStatus.Active);
    }

    [Fact]
    public async Task Negociacion_ReusesAClient_StampsTheOriginLead()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = CreateLead(dealerId, email: "origen@test.com");
        var existingClient = new Client(dealerId, "Ana", "Lopez", "20444555", "origen@test.com", "4445556", "Addr", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(existingClient);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider());
        await handler.Handle(
            new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion),
            CancellationToken.None);

        var client = await context.Clients.SingleAsync();
        client.OriginLeadId.Should().Be(
            lead.Id,
            "every rule that walks from a client back to its enquiry sees nothing without this stamp");
    }
}
