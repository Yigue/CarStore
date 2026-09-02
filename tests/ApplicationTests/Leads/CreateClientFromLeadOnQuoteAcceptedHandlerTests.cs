using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.CreateClient;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Quotes.Events;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

/// <summary>
/// Covers CreateClientFromLeadOnQuoteAcceptedHandler: the Ganado-path handler that converts
/// a Lead into a Client when its Quote is accepted. REQ-CRM-DEDUP-001 / ADR-4 requires
/// checking Lead.ConvertedClientId first (reusing any Prospect Client already created at
/// Negociación) before falling back to the legacy email-match find-or-create, and calling
/// Client.Activate() (ADR-2) once the target Client is resolved.
/// </summary>
public class CreateClientFromLeadOnQuoteAcceptedHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Car CreateCar(TestApplicationDbContext context, Guid dealerId, string plate)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, plate, "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    [Fact]
    public async Task Handle_NoExistingClient_CreatesClientAndActivatesIt()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Carlos Perez", "carlos@test.com", "1112223", LeadSource.Web, DateTime.UtcNow);
        context.Leads.Add(lead);
        var car = CreateCar(context, dealerId, "QAH001");
        await context.SaveChangesAsync();

        var quote = new Quote(dealerId, car, null, lead, 100_000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(5), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnQuoteAcceptedHandler(context, new FakeDateTimeProvider());

        await handler.Handle(new QuoteAcceptedDomainEvent(quote.Id), CancellationToken.None);

        var client = await context.Clients.SingleAsync();
        client.Status.Should().Be(ClientStatus.Active);

        var updatedLead = await context.Leads.FindAsync(lead.Id);
        updatedLead!.ConvertedClientId.Should().Be(client.Id);

        var updatedQuote = await context.Quotes.FindAsync(quote.Id);
        updatedQuote!.ClientId.Should().Be(client.Id);
        updatedQuote.LeadId.Should().NotBeNull("the quote keeps the enquiry it came from after conversion");
    }

    [Fact]
    public async Task Handle_EmailMatchesExistingClient_ReusesItAndActivates()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Ana Lopez", "ana@test.com", "4445556", LeadSource.Web, DateTime.UtcNow);
        var existingClient = new Client(dealerId, "Ana", "Lopez", "20444555", "ana@test.com", "4445556", "Some address", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(existingClient);
        var car = CreateCar(context, dealerId, "QAH002");
        await context.SaveChangesAsync();

        var quote = new Quote(dealerId, car, null, lead, 100_000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(5), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnQuoteAcceptedHandler(context, new FakeDateTimeProvider());

        await handler.Handle(new QuoteAcceptedDomainEvent(quote.Id), CancellationToken.None);

        var clientCount = await context.Clients.CountAsync();
        clientCount.Should().Be(1, "no duplicate should be created when an email match exists");

        var reused = await context.Clients.SingleAsync();
        reused.Id.Should().Be(existingClient.Id);
        reused.Status.Should().Be(ClientStatus.Active);
    }

    [Fact]
    public async Task Handle_LeadAlreadyHasProspectClient_ReusesItWithoutActivating()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var lead = Lead.Create(dealerId, "Pedro Ramirez", "pedro@test.com", "5556667", LeadSource.Web, DateTime.UtcNow);
        var prospectClient = new Client(dealerId, "Pedro", "Ramirez", "TEMP0001", "pedro@test.com", "5556667", string.Empty, DateTime.UtcNow, ClientType.Individual, lead.Id);
        prospectClient.SetProspect();
        context.Leads.Add(lead);
        context.Clients.Add(prospectClient);
        await context.SaveChangesAsync();

        lead.MarkConverted(prospectClient.Id);
        var car = CreateCar(context, dealerId, "QAH003");
        await context.SaveChangesAsync();

        var quote = new Quote(dealerId, car, null, lead, 100_000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(5), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnQuoteAcceptedHandler(context, new FakeDateTimeProvider());

        await handler.Handle(new QuoteAcceptedDomainEvent(quote.Id), CancellationToken.None);

        var clientCount = await context.Clients.CountAsync();
        clientCount.Should().Be(1, "the Prospect Client created at Negociación must be reused, not duplicated");

        var reused = await context.Clients.SingleAsync();
        reused.Id.Should().Be(prospectClient.Id);
        reused.Status.Should().Be(ClientStatus.Prospect, "accepting a quote does not activate the client; it remains Prospect until sale completed");
    }

    [Fact]
    public async Task Handle_QuoteAlreadyHasClient_IsNoOp()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, "Existing", "Client", "10000000", "existing@test.com", "111", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        var car = CreateCar(context, dealerId, "QAH004");
        await context.SaveChangesAsync();

        var quote = new Quote(dealerId, car, client, null, 100_000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(5), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var handler = new CreateClientFromLeadOnQuoteAcceptedHandler(context, new FakeDateTimeProvider());

        await handler.Handle(new QuoteAcceptedDomainEvent(quote.Id), CancellationToken.None);

        var clientCount = await context.Clients.CountAsync();
        clientCount.Should().Be(1, "a quote already linked to a client must not trigger client creation/activation");
    }
}
