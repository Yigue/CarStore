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
using Domain.Quotes.Events;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Leads;

/// <summary>
/// Domain events are not published in-process. SaveChangesAsync writes them to the outbox and
/// <c>ProcessOutboxMessagesJob</c> dispatches them later, with no HTTP context and therefore no
/// resolved tenant — <c>HasTenant</c> is false, so <b>every global query filter is disabled</b>
/// for the whole of a handler's execution. That is the normal state for these handlers, not an
/// edge case.
///
/// So a lookup by anything other than an id has to scope by DealerId itself. Sharing an email
/// across dealerships is ordinary — a buyer shops at several — and matching one dealership's lead
/// to another's client corrupts both records at once.
///
/// TestApplicationDbContext mirrors that state: it applies the entity configurations but not the
/// global filters, so an unscoped query here sees every dealer's rows.
/// </summary>
public class ClientFromLeadTenantScopeTests
{
    private const string SharedEmail = "shopper@test.com";

    private static readonly Guid DealerA = Guid.NewGuid();
    private static readonly Guid DealerB = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Client ForeignClient() =>
        new(DealerB, "Otro", "Comprador", "99", SharedEmail, "1", "Addr", DateTime.UtcNow);

    private static Lead LeadForDealerA() =>
        Lead.Create(DealerA, "Ana Fernandez", SharedEmail, "1", LeadSource.Web, DateTime.UtcNow);

    // ─── Negociación ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Negociacion_Should_NotAdoptAnotherDealersClient()
    {
        using var context = CreateContext();
        Client foreign = ForeignClient();
        Lead lead = LeadForDealerA();
        lead.ForceStatus(LeadStatus.Negociacion);

        context.Clients.Add(foreign);
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        await new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider())
            .Handle(
                new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Demostracion, LeadStatus.Negociacion),
                CancellationToken.None);

        Lead stored = await context.Leads.SingleAsync(l => l.Id == lead.Id);

        stored.ConvertedClientId.Should().NotBe(
            foreign.Id, "a lead must never be linked to another dealership's client");

        Client created = await context.Clients.SingleAsync(c => c.Id == stored.ConvertedClientId);
        created.DealerId.Should().Be(DealerA);
    }

    [Fact]
    public async Task Negociacion_Should_StillReuseItsOwnDealersClient()
    {
        using var context = CreateContext();
        var own = new Client(DealerA, "Ana", "Fernandez", "11", SharedEmail, "1", "Addr", DateTime.UtcNow);
        Lead lead = LeadForDealerA();
        lead.ForceStatus(LeadStatus.Negociacion);

        context.Clients.AddRange(own, ForeignClient());
        context.Leads.Add(lead);
        await context.SaveChangesAsync();

        await new CreateClientFromLeadOnNegociacionHandler(context, new FakeDateTimeProvider())
            .Handle(
                new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Demostracion, LeadStatus.Negociacion),
                CancellationToken.None);

        (await context.Leads.SingleAsync(l => l.Id == lead.Id))
            .ConvertedClientId.Should().Be(own.Id, "scoping must not stop it reusing its own client");

        context.Clients.Count(c => c.DealerId == DealerA)
            .Should().Be(1, "and it must not duplicate that client either");
    }

    // ─── Cotización aceptada ───────────────────────────────────────────────────

    [Fact]
    public async Task QuoteAccepted_Should_NotAdoptAnotherDealersClient()
    {
        using var context = CreateContext();
        Client foreign = ForeignClient();
        Lead lead = LeadForDealerA();

        var marca = new Marca("Fiat");
        var modelo = new Modelo("Cronos", marca.Id);
        var car = new Car(
            DealerA, marca, modelo, Color.Gray, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1300, 30000, 2021, "TEN001", "desc",
            9000m, DateTime.UtcNow);

        var quote = new Quote(
            DealerA, car, null, lead, 9000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);

        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(foreign);
        context.Leads.Add(lead);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await new CreateClientFromLeadOnQuoteAcceptedHandler(context, new FakeDateTimeProvider())
            .Handle(new QuoteAcceptedDomainEvent(quote.Id), CancellationToken.None);

        Lead stored = await context.Leads.SingleAsync(l => l.Id == lead.Id);

        stored.ConvertedClientId.Should().NotBe(foreign.Id);

        if (stored.ConvertedClientId is { } clientId)
        {
            (await context.Clients.SingleAsync(c => c.Id == clientId))
                .DealerId.Should().Be(DealerA);
        }
    }
}
