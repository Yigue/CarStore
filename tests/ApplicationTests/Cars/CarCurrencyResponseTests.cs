using Application.Abstractions.Authentication;
using Application.Abstractions.Storage;
using Application.Cars.GetAll;
using Application.Cars.GetById;
using Application.Cars.GetByIds;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Cars;

/// <summary>
/// sfc B0.3.3: every car read returns the price currency. Without it the edit form cannot default to the car's
/// currency, and saving a USD car would send ARS and silently convert it.
/// </summary>
public class CarCurrencyResponseTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static async Task<(TestApplicationDbContext Context, Guid CarId)> SeedUsdCarAsync()
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        var car = new Car(DealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1800, 0, 2026, "USD001", "d", 25000m, DateTime.UtcNow, priceCurrency: CurrencyCode.Parse("USD"));
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        return (context, car.Id);
    }

    [Fact]
    public async Task Detail_list_and_bulk_return_the_price_currency()
    {
        var (context, carId) = await SeedUsdCarAsync();
        var storage = new Mock<IStorageService>().Object;
        var user = new Mock<IUserContext>().Object;

        (await new GetCarByIdQueryHandler(context, storage, user).Handle(new GetCarByIdQuery(carId), CancellationToken.None))
            .Value.Currency.Should().Be("USD");
        (await new GetAllCarsQueryHandler(context, storage, user).Handle(new GetAllCarsQuery(1, 10), CancellationToken.None))
            .Value.Items.Single().Currency.Should().Be("USD");
        (await new GetCarsByIdsQueryHandler(context, storage, user).Handle(new GetCarsByIdsQuery([carId]), CancellationToken.None))
            .Value.Single().Currency.Should().Be("USD");
    }
}

/// <summary>sfc B0.3.3: the quote read returns its own currency, so editing a USD quote keeps USD.</summary>
public class QuoteCurrencyResponseTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task The_quote_detail_returns_its_currency()
    {
        var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        var car = new Car(DealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1800, 0, 2026, "USD002", "d", 25000m, DateTime.UtcNow, priceCurrency: CurrencyCode.Parse("USD"));
        var client = new Domain.Clients.Client(DealerId, "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", DateTime.UtcNow);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync();
        var quote = new Domain.Quotes.Quote(DealerId, car, client, null, 24000m, Domain.Quotes.Attributes.PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(10), "q", DateTime.UtcNow, CurrencyCode.Parse("USD"));
        context.Quotes.Add(quote);
        await context.SaveChangesAsync();

        var result = await new Application.Quotes.GetById.GetQuoteByIdQueryHandler(context)
            .Handle(new Application.Quotes.GetById.GetQuoteByIdQuery(quote.Id), CancellationToken.None);

        result.Value.Currency.Should().Be("USD");
    }
}
