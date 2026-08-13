using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Cars.GetAll;
using Application.Abstractions.Authentication;
using Application.Abstractions.Storage;
using Domain.Cars;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using SharedKernel;

namespace Application.UnitTests.Cars;

/// <summary>
/// Guards the sort contract of <c>GET /api/v1/cars</c>. Before this suite the endpoint
/// accepted <c>sortBy</c>/<c>sortOrder</c> and silently ignored both: every combination
/// returned the same order and an unknown field still answered 200.
/// </summary>
public class GetAllCarsSortingTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Car BuildCar(
        Marca marca,
        Modelo modelo,
        string patente,
        decimal price,
        int anio,
        int kilometraje,
        DateTime createdAt,
        FuelType fuelType = FuelType.Gasolina,
        Transmission transmission = Transmission.Manual) =>
        new(
            Guid.NewGuid(),
            marca,
            modelo,
            Color.Blue,
            TypeCar.Sedan,
            StatusCar.Used,
            StatusServiceCar.Disponible,
            4,
            5,
            1600,
            kilometraje,
            anio,
            patente,
            "Test car",
            price,
            createdAt,
            fuelType,
            false,
            transmission,
            null);

    /// <summary>
    /// Seeds three cars whose price, year, mileage and creation order all differ, so a
    /// wrong sort key can never accidentally produce the expected sequence.
    /// </summary>
    private static async Task<(TestApplicationDbContext Context, Car Cheap, Car Mid, Car Expensive)> SeedAsync()
    {
        var context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // price asc: cheap < mid < expensive
        // anio asc: expensive < cheap < mid
        // createdAt asc: mid < expensive < cheap
        var cheap = BuildCar(marca, modelo, "AAA111", 10_000m, 2020, 90_000, baseDate.AddDays(3));
        var mid = BuildCar(marca, modelo, "BBB222", 20_000m, 2023, 40_000, baseDate.AddDays(1), FuelType.Diesel, Transmission.Automatic);
        var expensive = BuildCar(marca, modelo, "CCC333", 30_000m, 2018, 10_000, baseDate.AddDays(2), FuelType.Electrico, Transmission.CVT);

        context.Cars.AddRange(cheap, mid, expensive);
        await context.SaveChangesAsync();

        return (context, cheap, mid, expensive);
    }

    /// <summary>Admin: estos tests miran el ORDEN, no quién ve el costo.</summary>
    private static GetAllCarsQueryHandler CreateHandler(TestApplicationDbContext context)
    {
        var adminContext = new Mock<IUserContext>();
        adminContext.Setup(x => x.IsAdmin).Returns(true);
        return new GetAllCarsQueryHandler(context, new Mock<IStorageService>().Object, adminContext.Object);
    }

    [Fact]
    public async Task Handle_Should_SortByPriceAscending_WhenSortOrderIsAsc()
    {
        var (context, cheap, mid, expensive) = await SeedAsync();
        using var _ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, "price", "asc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(c => c.Id)
            .Should().ContainInOrder(cheap.Id, mid.Id, expensive.Id);
    }

    [Fact]
    public async Task Handle_Should_SortByPriceDescending_WhenSortOrderIsDesc()
    {
        var (context, cheap, mid, expensive) = await SeedAsync();
        using var _ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, "price", "desc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(c => c.Id)
            .Should().ContainInOrder(expensive.Id, mid.Id, cheap.Id);
    }

    [Fact]
    public async Task Handle_Should_ProduceDifferentOrder_ForAscAndDesc()
    {
        var (context, _, _, _) = await SeedAsync();
        using var __ = context;

        var handler = CreateHandler(context);
        var asc = await handler.Handle(new GetAllCarsQuery(1, 10, "price", "asc"), CancellationToken.None);
        var desc = await handler.Handle(new GetAllCarsQuery(1, 10, "price", "desc"), CancellationToken.None);

        asc.Value.Items.Select(c => c.Id)
            .Should().NotEqual(desc.Value.Items.Select(c => c.Id));
    }

    [Theory]
    [InlineData("anio")]
    [InlineData("year")]
    public async Task Handle_Should_SortByYear_WhenSortByIsYearAlias(string sortBy)
    {
        var (context, cheap, mid, expensive) = await SeedAsync();
        using var _ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, sortBy, "asc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(c => c.Id)
            .Should().ContainInOrder(expensive.Id, cheap.Id, mid.Id);
    }

    [Fact]
    public async Task Handle_Should_SortByKilometraje_WhenSortByIsKilometraje()
    {
        var (context, cheap, mid, expensive) = await SeedAsync();
        using var _ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, "kilometraje", "asc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(c => c.Id)
            .Should().ContainInOrder(expensive.Id, mid.Id, cheap.Id);
    }

    [Fact]
    public async Task Handle_Should_SortByFuelType_WhenSortByIsFuelType()
    {
        var (context, cheap, mid, expensive) = await SeedAsync();
        using var _ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, "fuelType", "asc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Enum order: Gasolina(0) < Diesel(1) < Electrico(2)
        result.Value.Items.Select(c => c.Id)
            .Should().ContainInOrder(cheap.Id, mid.Id, expensive.Id);
    }

    [Fact]
    public async Task Handle_Should_DefaultToNewestFirst_WhenNoSortRequested()
    {
        var (context, cheap, mid, expensive) = await SeedAsync();
        using var _ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(c => c.Id)
            .Should().ContainInOrder(cheap.Id, expensive.Id, mid.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnValidationError_WhenSortByIsUnknown()
    {
        var (context, _, _, _) = await SeedAsync();
        using var __ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, "NotARealField", "asc"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Description.Should().Contain("NotARealField");
    }

    [Fact]
    public async Task Handle_Should_ReturnValidationError_WhenSortOrderIsUnknown()
    {
        var (context, _, _, _) = await SeedAsync();
        using var __ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, "price", "sideways"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Handle_Should_AcceptSortByCaseInsensitively()
    {
        var (context, cheap, _, expensive) = await SeedAsync();
        using var __ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10, "PRICE", "DESC"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.First().Id.Should().Be(expensive.Id);
        result.Value.Items.Last().Id.Should().Be(cheap.Id);
    }

    /// <summary>
    /// The sort must be applied to the whole result set before paging. Sorting only the
    /// rows of the current page is the classic defect this asserts against: page 1 of a
    /// price-descending sort must hold the single most expensive car, not the first row
    /// of the unsorted page re-ordered in place.
    /// </summary>
    [Fact]
    public async Task Handle_Should_SortAcrossFullResultSet_BeforePaging()
    {
        var (context, cheap, mid, expensive) = await SeedAsync();
        using var _ = context;

        var handler = CreateHandler(context);

        var page1 = await handler.Handle(new GetAllCarsQuery(1, 1, "price", "desc"), CancellationToken.None);
        var page2 = await handler.Handle(new GetAllCarsQuery(2, 1, "price", "desc"), CancellationToken.None);
        var page3 = await handler.Handle(new GetAllCarsQuery(3, 1, "price", "desc"), CancellationToken.None);

        page1.Value.Items.Should().ContainSingle().Which.Id.Should().Be(expensive.Id);
        page2.Value.Items.Should().ContainSingle().Which.Id.Should().Be(mid.Id);
        page3.Value.Items.Should().ContainSingle().Which.Id.Should().Be(cheap.Id);
        page1.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_Should_ProjectFuelTypeAndTransmission()
    {
        var (context, cheap, mid, expensive) = await SeedAsync();
        using var _ = context;

        var result = await CreateHandler(context).Handle(
            new GetAllCarsQuery(1, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        result.Value.Items.Single(c => c.Id == cheap.Id).FuelType.Should().Be(FuelType.Gasolina);
        result.Value.Items.Single(c => c.Id == mid.Id).FuelType.Should().Be(FuelType.Diesel);
        result.Value.Items.Single(c => c.Id == expensive.Id).FuelType.Should().Be(FuelType.Electrico);

        result.Value.Items.Single(c => c.Id == cheap.Id).Transmission.Should().Be(Transmission.Manual);
        result.Value.Items.Single(c => c.Id == mid.Id).Transmission.Should().Be(Transmission.Automatic);
        result.Value.Items.Single(c => c.Id == expensive.Id).Transmission.Should().Be(Transmission.CVT);
    }
}
