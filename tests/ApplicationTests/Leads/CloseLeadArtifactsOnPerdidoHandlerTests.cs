using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Leads.CloseArtifacts;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Leads;
using Domain.Leads.Events;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QuotePaymentMethod = Domain.Quotes.Attributes.PaymentMethod;
using SalePaymentMethod = Domain.Financial.Attributes.PaymentMethod;
using SaleStatus = Domain.Sales.Attributes.SaleStatus;

namespace Application.UnitTests.Leads;

/// <summary>
/// LEAD-04: losing a lead closes what was hanging off it.
///
/// <para>
/// The worst case was never untidiness. An ACCEPTED quote holds its vehicle Reservado, so a deal
/// that no longer existed kept a car off the floor and nothing in the system ever noticed.
/// </para>
/// </summary>
public class CloseLeadArtifactsOnPerdidoHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CloseLeadArtifactsOnPerdidoHandler CreateHandler(TestApplicationDbContext context) =>
        new(context,
            new FakeDateTimeProvider { UtcNow = new DateTime(2024, 6, 1) },
            NullLogger<CloseLeadArtifactsOnPerdidoHandler>.Instance);

    private static Lead CreateLead() =>
        Lead.Create(DealerId, "Carlos Perez", "carlos@test.com", "1112223", LeadSource.Web, DateTime.UtcNow);

    private static Car SeedCar(TestApplicationDbContext context, string patente, StatusServiceCar status)
    {
        var marca = new Marca($"Marca{patente}");
        var modelo = new Modelo($"Modelo{patente}", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, status,
            4, 5, 1600, 1000, 2021, patente, "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    private static LeadStatusChangedDomainEvent PerdidoEvent(Guid leadId) =>
        new(leadId, LeadStatus.Negociacion, LeadStatus.Perdido);

    [Fact]
    public async Task Perdido_RejectsThePendingQuote_AndKeepsTheRecord()
    {
        using var context = CreateContext();
        Lead lead = CreateLead();
        context.Leads.Add(lead);
        Car car = SeedCar(context, "PND111", StatusServiceCar.Disponible);
        await context.SaveChangesAsync();

        var quote = new Quote(DealerId, car, null, lead, 100_000m, QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(PerdidoEvent(lead.Id), CancellationToken.None);

        Quote persisted = await context.Quotes.SingleAsync();
        persisted.Status.Should().Be(QuoteStatus.Rejected);
        (await context.Quotes.CountAsync()).Should().Be(1, "the record is closed, never deleted");
    }

    // The one that actually cost money: an accepted quote holds the car.
    [Fact]
    public async Task Perdido_ClosesTheAcceptedQuote_AndPutsTheCarBackOnTheFloor()
    {
        using var context = CreateContext();
        Lead lead = CreateLead();
        context.Leads.Add(lead);
        Car car = SeedCar(context, "ACC111", StatusServiceCar.Disponible);
        await context.SaveChangesAsync();

        var quote = new Quote(DealerId, car, null, lead, 100_000m, QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        quote.Accept(DateTime.UtcNow);
        car.Reserve(DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(PerdidoEvent(lead.Id), CancellationToken.None);

        (await context.Quotes.SingleAsync()).Status.Should().Be(QuoteStatus.Rejected);
        (await context.Cars.SingleAsync(c => c.Id == car.Id)).ServiceCar.Should().Be(
            StatusServiceCar.Disponible,
            "a deal that no longer exists must not keep a unit off the floor");
    }

    [Fact]
    public async Task Perdido_CancelsThePendingSale()
    {
        using var context = CreateContext();
        Lead lead = CreateLead();
        var client = new Client(DealerId, "Carlos", "Perez", "20111222", "carlos@test.com", "111", "Addr", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(client);
        Car car = SeedCar(context, "SAL111", StatusServiceCar.Reservado);
        await context.SaveChangesAsync();

        var sale = new Sale(DealerId, car.Id, client.Id, 100_000m, SalePaymentMethod.Cash, "CN-1", "", DateTime.UtcNow, lead.Id);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(PerdidoEvent(lead.Id), CancellationToken.None);

        Sale persisted = await context.Sales.SingleAsync();
        persisted.Status.Should().Be(SaleStatus.Cancelled);
        (await context.Cars.SingleAsync(c => c.Id == car.Id)).ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }

    // Money changed hands. A pipeline drag does not undo that.
    [Fact]
    public async Task Perdido_LeavesACompletedSaleAlone()
    {
        using var context = CreateContext();
        Lead lead = CreateLead();
        var client = new Client(DealerId, "Carlos", "Perez", "20111222", "carlos@test.com", "111", "Addr", DateTime.UtcNow);
        context.Leads.Add(lead);
        context.Clients.Add(client);
        Car car = SeedCar(context, "CMP111", StatusServiceCar.Vendido);
        await context.SaveChangesAsync();

        var sale = new Sale(DealerId, car.Id, client.Id, 100_000m, SalePaymentMethod.Cash, "CN-1", "", DateTime.UtcNow, lead.Id);
        sale.Complete();
        context.Sales.Add(sale);

        var quote = new Quote(DealerId, car, client, lead, 100_000m, QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(PerdidoEvent(lead.Id), CancellationToken.None);

        (await context.Sales.SingleAsync()).Status.Should().Be(SaleStatus.Completed);
        (await context.Quotes.SingleAsync()).Status.Should().Be(
            QuoteStatus.Pending,
            "nothing is closed for a lead that already bought — that state is a data-entry mistake, not an instruction");
        (await context.Cars.SingleAsync(c => c.Id == car.Id)).ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }

    // The paperwork raised after the conversion hangs off the client, not the lead.
    [Fact]
    public async Task Perdido_ReachesTheQuotesThatHangOffTheConvertedClient()
    {
        using var context = CreateContext();
        Lead lead = CreateLead();
        var client = new Client(DealerId, "Carlos", "Perez", "20111222", "carlos@test.com", "111", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        context.Leads.Add(lead);
        lead.MarkConverted(client.Id);
        Car car = SeedCar(context, "CLI111", StatusServiceCar.Disponible);
        await context.SaveChangesAsync();

        // Raised against the client alone — no LeadId.
        var quote = new Quote(DealerId, car, client, null, 100_000m, QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(PerdidoEvent(lead.Id), CancellationToken.None);

        (await context.Quotes.SingleAsync()).Status.Should().Be(QuoteStatus.Rejected);
    }

    [Fact]
    public async Task Perdido_IsIdempotent_AcrossOutboxRetries()
    {
        using var context = CreateContext();
        Lead lead = CreateLead();
        context.Leads.Add(lead);
        Car car = SeedCar(context, "IDE111", StatusServiceCar.Disponible);
        await context.SaveChangesAsync();

        var quote = new Quote(DealerId, car, null, lead, 100_000m, QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        CloseLeadArtifactsOnPerdidoHandler handler = CreateHandler(context);
        await handler.Handle(PerdidoEvent(lead.Id), CancellationToken.None);
        await handler.Handle(PerdidoEvent(lead.Id), CancellationToken.None);

        (await context.Quotes.SingleAsync()).Status.Should().Be(QuoteStatus.Rejected);
    }

    // A car another live deal is holding stays held.
    [Fact]
    public async Task Perdido_DoesNotReleaseACarAnotherLiveQuoteStillHolds()
    {
        using var context = CreateContext();
        Lead lostLead = CreateLead();
        Lead otherLead = Lead.Create(DealerId, "Ana Lopez", "ana@test.com", "222", LeadSource.Web, DateTime.UtcNow);
        context.Leads.AddRange(lostLead, otherLead);
        Car car = SeedCar(context, "SHR111", StatusServiceCar.Disponible);
        await context.SaveChangesAsync();

        var lostQuote = new Quote(DealerId, car, null, lostLead, 100_000m, QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        var otherQuote = new Quote(DealerId, car, null, otherLead, 105_000m, QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        otherQuote.Accept(DateTime.UtcNow);
        car.Reserve(DateTime.UtcNow);
        context.Quotes.AddRange(lostQuote, otherQuote);
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(PerdidoEvent(lostLead.Id), CancellationToken.None);

        (await context.Quotes.SingleAsync(q => q.Id == lostQuote.Id)).Status.Should().Be(QuoteStatus.Rejected);
        (await context.Quotes.SingleAsync(q => q.Id == otherQuote.Id)).Status.Should().Be(QuoteStatus.Accepted);
        (await context.Cars.SingleAsync(c => c.Id == car.Id)).ServiceCar.Should().Be(
            StatusServiceCar.Reservado,
            "another buyer's accepted offer still holds this unit");
    }

    [Fact]
    public async Task NonPerdidoStatus_IsANoOp()
    {
        using var context = CreateContext();
        Lead lead = CreateLead();
        context.Leads.Add(lead);
        Car car = SeedCar(context, "NOP111", StatusServiceCar.Disponible);
        await context.SaveChangesAsync();

        var quote = new Quote(DealerId, car, null, lead, 100_000m, QuotePaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        await CreateHandler(context).Handle(
            new LeadStatusChangedDomainEvent(lead.Id, LeadStatus.Contactado, LeadStatus.Negociacion),
            CancellationToken.None);

        (await context.Quotes.SingleAsync()).Status.Should().Be(QuoteStatus.Pending);
    }
}
