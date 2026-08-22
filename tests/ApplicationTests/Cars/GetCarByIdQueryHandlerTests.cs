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

    /// <summary>
    /// La patente sólo viaja a un llamador autenticado. GET /cars/{id} es
    /// AllowAnonymous porque lo consume la ficha del catálogo público, así que
    /// sin este corte la patente — que identifica un vehículo físico concreto —
    /// salía a cualquiera que pidiera el id, y la card del catálogo hasta la
    /// metía en el título de compartir. El corte va en "logueado" y no en
    /// "admin" porque un vendedor la necesita.
    ///
    /// Sin este test alguien podría sacar el ternario del handler y volver a
    /// exponerla sin que nada se ponga en rojo.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_Should_ReturnPatente_OnlyToAuthenticatedCallers(bool isAuthenticated)
    {
        using var context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        var car = new Car(
            Guid.NewGuid(), marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 10_000, 2022, "PAT001", "Test car",
            25000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);

        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var userContext = new Mock<IUserContext>();
        userContext.Setup(x => x.IsAuthenticated).Returns(isAuthenticated);

        var handler = new GetCarByIdQueryHandler(context, new Mock<IStorageService>().Object, userContext.Object);

        var result = await handler.Handle(new GetCarByIdQuery(car.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        if (isAuthenticated)
        {
            result.Value.Patente.Should().Be("PAT001");
        }
        else
        {
            result.Value.Patente.Should().BeNull();
        }
    }
}
