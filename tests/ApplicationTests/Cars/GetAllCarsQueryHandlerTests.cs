using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Cars.GetAll;
using Application.Abstractions.Storage;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;

namespace Application.UnitTests.Cars;

public class GetAllCarsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_ProjectPurchaseCostAndFeatured_ForListRows()
    {
        using var context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        var carWithCost = new Car(
            Guid.NewGuid(),
            marca,
            modelo,
            Color.Blue,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4,
            5,
            2000,
            0,
            2024,
            "XYZ123",
            "Test car",
            25000m,
            DateTime.UtcNow,
            FuelType.Gasolina,
            true,
            Transmission.Manual,
            12345m // PurchaseCost
        );
        
        var carWithoutCost = new Car(
            Guid.NewGuid(),
            marca,
            modelo,
            Color.Red,
            TypeCar.Hatchback,
            StatusCar.Used,
            StatusServiceCar.Disponible,
            5,
            5,
            1600,
            50000,
            2019,
            "ABC987",
            "Legacy car",
            10000m,
            DateTime.UtcNow,
            FuelType.Gasolina,
            false,
            Transmission.Manual,
            null // PurchaseCost
        );
        
        context.Cars.AddRange(carWithCost, carWithoutCost);
        await context.SaveChangesAsync();

        var mockStorage = new Mock<IStorageService>();
        var handler = new GetAllCarsQueryHandler(context, mockStorage.Object);

        var result = await handler.Handle(new GetAllCarsQuery(1, 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);

        var itemWithCost = result.Value.Items.Single(c => c.Id == carWithCost.Id);
        itemWithCost.Featured.Should().BeTrue();
        itemWithCost.PurchaseCost.Should().Be(12345m);

        var itemWithoutCost = result.Value.Items.Single(c => c.Id == carWithoutCost.Id);
        itemWithoutCost.Featured.Should().BeFalse();
        itemWithoutCost.PurchaseCost.Should().BeNull();
    }
}
