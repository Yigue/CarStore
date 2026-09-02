using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Quotes.Accept;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Quotes;

/// <summary>
/// Acceptance is the one exclusive moment in a car's life before the sale: several buyers may
/// hold competing offers on the same unit, but the dealership can only commit it to one of them.
/// The reservation used to happen when a quote was raised, which made the first offer exclusive
/// and the second one impossible. It lives here now.
/// </summary>
public class AcceptQuoteCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task<Car> SeedCarAsync(TestApplicationDbContext context)
    {
        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2021, "ACC001", "desc", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return car;
    }

    private static async Task<Client> SeedClientAsync(TestApplicationDbContext context, string dni, string email)
    {
        var client = new Client(DealerId, "Ada", "Lovelace", dni, email, "555", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        return client;
    }

    private static async Task<Quote> SeedQuoteAsync(TestApplicationDbContext context, Car car, Client client)
    {
        var quote = new Quote(DealerId, car, client, null, 14000m, PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(7), "comments", DateTime.UtcNow);
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();
        return quote;
    }

    [Fact]
    public async Task Handle_Should_ReserveTheCar_WhenTheQuoteIsAccepted()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var client = await SeedClientAsync(context, "111", "ada@test.com");
        var quote = await SeedQuoteAsync(context, car, client);
        var handler = new AcceptQuoteCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(new AcceptQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persistedCar = await context.Cars.SingleAsync(c => c.Id == car.Id);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    [Fact]
    public async Task Handle_Should_Refuse_WhenAnotherQuoteForTheSameCarIsAlreadyAccepted()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var winner = await SeedClientAsync(context, "111", "ada@test.com");
        var loser = await SeedClientAsync(context, "222", "grace@test.com");
        var acceptedQuote = await SeedQuoteAsync(context, car, winner);
        var competingQuote = await SeedQuoteAsync(context, car, loser);
        var handler = new AcceptQuoteCommandHandler(context, new FakeDateTimeProvider());

        await handler.Handle(new AcceptQuoteCommand(acceptedQuote.Id), CancellationToken.None);
        var result = await handler.Handle(new AcceptQuoteCommand(competingQuote.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(QuoteErrors.CarAlreadyCommitted(car.Id));

        // The losing offer stays exactly as it was — refusing an acceptance must not quietly
        // reject the quote on the operator's behalf.
        var persistedLoser = await context.Quotes.SingleAsync(q => q.Id == competingQuote.Id);
        persistedLoser.Status.Should().Be(QuoteStatus.Pending);
    }

    [Fact]
    public async Task Handle_Should_LeaveCompetingPendingQuotesUntouched_WhenOneIsAccepted()
    {
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        var winner = await SeedClientAsync(context, "111", "ada@test.com");
        var other = await SeedClientAsync(context, "222", "grace@test.com");
        var winning = await SeedQuoteAsync(context, car, winner);
        var competing = await SeedQuoteAsync(context, car, other);
        var handler = new AcceptQuoteCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(new AcceptQuoteCommand(winning.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Quotes.SingleAsync(q => q.Id == competing.Id)).Status.Should().Be(QuoteStatus.Pending);
    }

    [Fact]
    public async Task Handle_Should_NotThrow_WhenTheCarIsAlreadyReservedByThisDeal()
    {
        // Car.Reserve throws unless the car is Disponible, so a re-entrant accept (a retry after
        // a partial failure, an already-reserved unit) must not blow up on the reservation.
        using var context = CreateContext();
        var car = await SeedCarAsync(context);
        car.Reserve(DateTime.UtcNow);
        await context.SaveChangesAsync();

        var client = await SeedClientAsync(context, "111", "ada@test.com");
        var quote = await SeedQuoteAsync(context, car, client);
        var handler = new AcceptQuoteCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(new AcceptQuoteCommand(quote.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Cars.SingleAsync(c => c.Id == car.Id)).ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }
}
