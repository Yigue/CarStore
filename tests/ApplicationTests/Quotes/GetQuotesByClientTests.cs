using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Quotes.Get;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Quotes;

/// <summary>
/// The client detail screen shipped with a hardcoded "No hay cotizaciones para este cliente"
/// because the handler filtered by nothing and there was no way to ask for one client's quotes.
/// </summary>
public class GetQuotesByClientTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context, string patente)
    {
        var marca = new Marca($"Peugeot-{patente}");
        var modelo = new Modelo($"208-{patente}", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, patente, "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static Client BuildClient(string email, Guid? originLeadId = null) =>
        new(DealerId, "Ana", "Fernandez", "1", email, "2", "Addr", DateTime.UtcNow,
            ClientType.Individual, originLeadId);

    private static Quote QuoteFor(Car car, Client? client, Lead? lead) =>
        new(DealerId, car, client, lead, 9000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(30), "", DateTime.UtcNow);

    [Fact]
    public async Task Handle_Should_ReturnEverything_WhenNoFilterIsGiven()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "ALL001");
        Client a = BuildClient("a@test.com");
        Client b = BuildClient("b@test.com");
        context.Clients.AddRange(a, b);
        context.Quotes.AddRange(QuoteFor(car, a, null), QuoteFor(car, b, null));
        await context.SaveChangesAsync();

        var result = await new GetQuotesQueryHandler(context)
            .Handle(new GetQuotesQuery(), CancellationToken.None);

        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyThatClientsQuotes()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "CLI001");
        Client mine = BuildClient("mine@test.com");
        Client other = BuildClient("other@test.com");
        context.Clients.AddRange(mine, other);
        context.Quotes.AddRange(QuoteFor(car, mine, null), QuoteFor(car, other, null));
        await context.SaveChangesAsync();

        var result = await new GetQuotesQueryHandler(context)
            .Handle(new GetQuotesQuery(ClientId: mine.Id), CancellationToken.None);

        result.Value.Should().ContainSingle()
            .Which.ClientId.Should().Be(mine.Id);
    }

    /// <summary>
    /// A quote raised after enquiries started creating leads hangs off the lead, not the client.
    /// It is still this client's history once the lead is converted, and dropping it would leave
    /// the tab looking empty for exactly the customers who came through the funnel properly.
    /// </summary>
    [Fact]
    public async Task Handle_Should_IncludeQuotesHeldByTheLeadTheClientCameFrom()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "LEA001");

        var lead = Lead.Create(DealerId, "Ana Fernandez", "ana@test.com", "1",
            LeadSource.Web, DateTime.UtcNow);
        Client client = BuildClient("ana@test.com", originLeadId: lead.Id);

        // Client does not assign its own Id — unlike Lead.Create — so it has to be persisted
        // before anything can reference it.
        context.Leads.Add(lead);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        lead.MarkConverted(client.Id);
        context.Quotes.Add(QuoteFor(car, null, lead));
        await context.SaveChangesAsync();

        var result = await new GetQuotesQueryHandler(context)
            .Handle(new GetQuotesQuery(ClientId: client.Id), CancellationToken.None);

        result.Value.Should().ContainSingle()
            .Which.LeadId.Should().Be(lead.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmpty_ForAClientWithNoQuotes()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "EMP001");
        Client withQuote = BuildClient("has@test.com");
        Client without = BuildClient("none@test.com");
        context.Clients.AddRange(withQuote, without);
        context.Quotes.Add(QuoteFor(car, withQuote, null));
        await context.SaveChangesAsync();

        var result = await new GetQuotesQueryHandler(context)
            .Handle(new GetQuotesQuery(ClientId: without.Id), CancellationToken.None);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_FilterByLead()
    {
        using var context = CreateContext();
        Car car = await SeedCarAsync(context, "LID001");
        var mine = Lead.Create(DealerId, "Mío", "mio@test.com", "1", LeadSource.Web, DateTime.UtcNow);
        var other = Lead.Create(DealerId, "Otro", "otro@test.com", "1", LeadSource.Web, DateTime.UtcNow);
        context.Leads.AddRange(mine, other);
        context.Quotes.AddRange(QuoteFor(car, null, mine), QuoteFor(car, null, other));
        await context.SaveChangesAsync();

        var result = await new GetQuotesQueryHandler(context)
            .Handle(new GetQuotesQuery(LeadId: mine.Id), CancellationToken.None);

        result.Value.Should().ContainSingle()
            .Which.LeadId.Should().Be(mine.Id);
    }
}
