using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Cars.GetById;
using Application.Abstractions.Authentication;
using Application.Abstractions.Storage;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;

namespace Application.UnitTests.Cars;

public class GetCarByIdQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_ProjectPurchaseCostAndFeatured_WhenPresent()
    {
        using var context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        var car = new Car(
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
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var mockStorage = new Mock<IStorageService>();
        // Estos tests verifican la PROYECCIÓN, no la autorización: un contexto de
        // admin para que PurchaseCost llegue al DTO. El caso no-admin (costo en
        // null) tiene su propio test.
        var adminContext = new Mock<IUserContext>();
        adminContext.Setup(x => x.IsAdmin).Returns(true);
        var handler = new GetCarByIdQueryHandler(context, mockStorage.Object, adminContext.Object);

        var result = await handler.Handle(new GetCarByIdQuery(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Featured.Should().BeTrue();
        result.Value.PurchaseCost.Should().Be(12345m);
    }

    [Fact]
    public async Task Handle_Should_ProjectNullPurchaseCost_ForLegacyRow()
    {
        using var context = CreateContext();
        var marca = new Marca("Ford");
        var modelo = new Modelo("Fiesta", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        var car = new Car(
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
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var mockStorage = new Mock<IStorageService>();
        // Estos tests verifican la PROYECCIÓN, no la autorización: un contexto de
        // admin para que PurchaseCost llegue al DTO. El caso no-admin (costo en
        // null) tiene su propio test.
        var adminContext = new Mock<IUserContext>();
        adminContext.Setup(x => x.IsAdmin).Returns(true);
        var handler = new GetCarByIdQueryHandler(context, mockStorage.Object, adminContext.Object);

        var result = await handler.Handle(new GetCarByIdQuery(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Featured.Should().BeFalse();
        result.Value.PurchaseCost.Should().BeNull();
    }
}
