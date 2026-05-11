using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Cars.GetInventoryStats;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Cars;

public class GetInventoryStatsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task SeedTestDataAsync(TestApplicationDbContext context)
    {
        var dealerId = Guid.NewGuid();
        var marca1 = new Marca("Toyota");
        var marca2 = new Marca("Ford");
        context.Marca.Add(marca1);
        context.Marca.Add(marca2);
        await context.SaveChangesAsync();

        var modelo1 = new Modelo("Corolla", marca1.Id);
        var modelo2 = new Modelo("Fiesta", marca2.Id);
        context.Modelo.Add(modelo1);
        context.Modelo.Add(modelo2);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var cars = new[]
        {
            new Car(dealerId, marca1, modelo1, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "ABC123", "Car 1", 20000m, now),
            new Car(dealerId, marca1, modelo1, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 2000, 2021, "DEF456", "Car 2", 25000m, now),
            new Car(dealerId, marca2, modelo2, Color.Black, TypeCar.Hatchback, StatusCar.Used, StatusServiceCar.EnVenta, 4, 5, 1400, 5000, 2019, "GHI789", "Car 3", 15000m, now),
            new Car(dealerId, marca1, modelo1, Color.White, TypeCar.SUV, StatusCar.New, StatusServiceCar.Vendido, 4, 7, 2000, 0, 2022, "JKL012", "Car 4", 35000m, now),
            new Car(dealerId, marca2, modelo2, Color.Green, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Vendido, 4, 5, 1800, 3000, 2020, "MNO345", "Car 5", 22000m, now),
        };

        context.Cars.AddRange(cars);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCorrectTotals_WhenCarsExist()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetInventoryStatsQueryHandler(context);
        var query = new GetInventoryStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(5);
        result.Value.TotalValue.Should().Be(117000m); // 20000 + 25000 + 15000 + 35000 + 22000
    }

    [Fact]
    public async Task Handle_ReturnsZeros_WhenNoCarsExist()
    {
        using var context = CreateContext();
        var handler = new GetInventoryStatsQueryHandler(context);
        var query = new GetInventoryStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(0);
        result.Value.TotalValue.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByStatus_ComputesCorrectCounts()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetInventoryStatsQueryHandler(context);
        var query = new GetInventoryStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ByStatus.Available.Should().Be(2); // Disponible
        result.Value.ByStatus.Reserved.Should().Be(1); // EnVenta
        result.Value.ByStatus.Sold.Should().Be(2); // Vendido
    }

    [Fact]
    public async Task Handle_ByBrand_GroupsCorrectly()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetInventoryStatsQueryHandler(context);
        var query = new GetInventoryStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ByBrand.Should().ContainKey("Toyota");
        result.Value.ByBrand.Should().ContainKey("Ford");
        result.Value.ByBrand["Toyota"].Should().Be(3);
        result.Value.ByBrand["Ford"].Should().Be(2);
    }

    [Fact]
    public async Task Handle_ByType_GroupsCorrectly()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetInventoryStatsQueryHandler(context);
        var query = new GetInventoryStatsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ByType.Should().ContainKey("Sedan");
        result.Value.ByType.Should().ContainKey("Hatchback");
        result.Value.ByType.Should().ContainKey("SUV");
        result.Value.ByType["Sedan"].Should().Be(3);
        result.Value.ByType["Hatchback"].Should().Be(1);
        result.Value.ByType["SUV"].Should().Be(1);
    }
}