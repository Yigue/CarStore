using Application.Abstractions.Storage;
using Application.Cars.Search;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Cars.Search;

/// <summary>
/// The public catalogue was listing sold units alongside the ones for sale.
///
/// <para>
/// <c>cars/search</c> is the AllowAnonymous endpoint the catalogue reads from, and it filtered by
/// brand, model, year, price, colour, type and doors — but never by whether the unit could still be
/// bought. Publishing a sold car as if it were available wastes the buyer's time and the
/// salesperson's.
/// </para>
///
/// <para>
/// The filter is opt-in so the dashboard keeps seeing the whole fleet: inventory has to show what
/// was sold.
/// </para>
/// </summary>
public class SearchCarsAvailabilityFilterTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Car SeedCar(TestApplicationDbContext context, string patente, StatusServiceCar status)
    {
        var marca = new Marca($"Marca{patente}");
        var modelo = new Modelo($"Modelo{patente}", marca.Id);
        var car = new Car(
            DealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, status,
            4, 5, 2000, 0, 2024, patente, "catalogue test car", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    private static SearchCarsQueryHandler CreateHandler(TestApplicationDbContext context)
    {
        var storage = new Mock<IStorageService>();
        return new SearchCarsQueryHandler(
            context,
            storage.Object,
            NullLogger<SearchCarsQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Should_HideSoldCars_WhenOnlyPurchasableIsRequested()
    {
        using var context = CreateContext();
        Car available = SeedCar(context, "AVA111", StatusServiceCar.Disponible);
        SeedCar(context, "SLD111", StatusServiceCar.Vendido);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new SearchCarsQuery { OnlyPurchasable = true },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Cars.Select(c => c.Id).Should().ContainSingle().And.Contain(available.Id);
    }

    [Fact]
    public async Task Handle_Should_CountOnlyVisibleCars_InTheTotal()
    {
        // A total that counts what the page does not show breaks paging: the catalogue promises a
        // second page that comes back empty.
        using var context = CreateContext();
        SeedCar(context, "AVA222", StatusServiceCar.Disponible);
        SeedCar(context, "SLD222", StatusServiceCar.Vendido);
        SeedCar(context, "SLD333", StatusServiceCar.Vendido);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new SearchCarsQuery { OnlyPurchasable = true },
            CancellationToken.None);

        result.Value.TotalResults.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnEverything_WhenTheFilterIsNotRequested()
    {
        // The dashboard's inventory has to keep seeing sold units.
        using var context = CreateContext();
        SeedCar(context, "AVA333", StatusServiceCar.Disponible);
        SeedCar(context, "SLD444", StatusServiceCar.Vendido);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new SearchCarsQuery(),
            CancellationToken.None);

        result.Value.Cars.Should().HaveCount(2);
    }

    // CAT-01: the column says what the dealership intends, the sale says what happened. Rows
    // written before the in-transaction sync — and any window where the outbox is behind or its
    // message errored — have a completed sale against a car still flagged Disponible. The
    // catalogue must believe the sale.
    [Fact]
    public async Task Handle_Should_HideACarWithACompletedSale_EvenWhenItsStatusWasNeverSynced()
    {
        using var context = CreateContext();
        Car available = SeedCar(context, "AVA444", StatusServiceCar.Disponible);
        Car soldButUnsynced = SeedCar(context, "STA111", StatusServiceCar.Disponible);
        await context.SaveChangesAsync();

        var sale = new Sale(
            DealerId, soldButUnsynced.Id, Guid.NewGuid(), 15000m,
            PaymentMethod.Cash, "C-1", "", DateTime.UtcNow);
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new SearchCarsQuery { OnlyPurchasable = true },
            CancellationToken.None);

        result.Value.Cars.Select(c => c.Id).Should().ContainSingle().And.Contain(available.Id);
        result.Value.TotalResults.Should().Be(1);
    }

    // A pending sale is a draft, not a delivery: the unit stays published while the deal can
    // still fall through, exactly like a Reservado one.
    [Fact]
    public async Task Handle_Should_KeepACarWithAPendingSale_InTheCatalogue()
    {
        using var context = CreateContext();
        SeedCar(context, "AVA555", StatusServiceCar.Disponible);
        Car reserved = SeedCar(context, "PEN001", StatusServiceCar.Reservado);
        await context.SaveChangesAsync();

        context.Sales.Add(new Sale(
            DealerId, reserved.Id, Guid.NewGuid(), 15000m,
            PaymentMethod.Cash, "C-2", "", DateTime.UtcNow));
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new SearchCarsQuery { OnlyPurchasable = true },
            CancellationToken.None);

        result.Value.Cars.Should().HaveCount(2);
    }
}
