using Application.Abstractions.Storage;
using Application.Cars.Search;
using Domain.Cars;
using Domain.Cars.Attributes;
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
}
