using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Caching;
using Application.Cars.Update;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Cars;

/// <summary>sfc B1.1 / RN-006 (corrects H5): a sold car only returns to Disponible via Sale.Void.</summary>
public class UpdateCarCommandHandlerGuardTests
{
    private static DbContextOptions<TestApplicationDbContext> Options(string db) =>
        new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(db).Options;

    private static async Task<(Guid CarId, Guid MarcaId, Guid ModeloId)> SeedAsync(
        string db, Guid dealerId, StatusServiceCar status, bool completedSale)
    {
        await using var seed = new TestApplicationDbContext(Options(db), dealerId);
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        seed.Marca.Add(marca);
        seed.Modelo.Add(modelo);
        var car = new Car(dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New,
            status, 4, 5, 2000, 0, 2020, "GRD123", "seed", 25000m, DateTime.UtcNow);
        seed.Cars.Add(car);
        if (completedSale)
        {
            var sale = new Sale(dealerId, car.Id, Guid.NewGuid(), 25000m, PaymentMethod.Cash, "CN-1", "", DateTime.UtcNow);
            sale.Complete();
            seed.Sales.Add(sale);
        }

        await seed.SaveChangesAsync();
        return (car.Id, marca.Id, modelo.Id);
    }

    private static async Task<Domain.Cars.Attributes.StatusServiceCar> StatusOf(string db, Guid dealerId, Guid carId)
    {
        await using var verify = new TestApplicationDbContext(Options(db), dealerId);
        return (await verify.Cars.SingleAsync(c => c.Id == carId)).ServiceCar;
    }

    private static UpdateCarCommandHandler Handler(TestApplicationDbContext ctx, Guid marcaId, Guid modeloId)
    {
        var brands = new Mock<ICachedBrandService>();
        brands.Setup(s => s.GetByIdAsync(marcaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarcaCacheDto { Id = marcaId, Nombre = "Toyota" });
        var models = new Mock<ICachedModelService>();
        models.Setup(s => s.GetByIdAsync(modeloId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModeloCacheDto { Id = modeloId, Nombre = "Corolla", MarcaId = marcaId });
        return new UpdateCarCommandHandler(ctx, new FakeDateTimeProvider { UtcNow = new DateTime(2024, 1, 1) },
            brands.Object, models.Object);
    }

    private static UpdateCarCommand Command(Guid carId, Guid marcaId, Guid modeloId, StatusServiceCar target) =>
        new(carId, marcaId, modeloId, Color.Blue, TypeCar.Sedan, StatusCar.New, target, 4, 5, 2000, 0, 2020,
            "GRD123", "seed", 25000m);

    [Fact]
    public async Task Handle_Should_Return409AndKeepCarSold_When_VendidoToDisponibleWithCompletedSale()
    {
        string db = Guid.NewGuid().ToString();
        var dealerId = Guid.NewGuid();
        var (carId, marcaId, modeloId) = await SeedAsync(db, dealerId, StatusServiceCar.Vendido, completedSale: true);

        await using var ctx = new TestApplicationDbContext(Options(db), dealerId);
        var result = await Handler(ctx, marcaId, modeloId)
            .Handle(Command(carId, marcaId, modeloId, StatusServiceCar.Disponible), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.SaleActiveBlocksStatusChange(carId));
        (await StatusOf(db, dealerId, carId)).Should().Be(StatusServiceCar.Vendido);
    }

    [Fact]
    public async Task Handle_Should_Allow_DisponibleToReservado()
    {
        string db = Guid.NewGuid().ToString();
        var dealerId = Guid.NewGuid();
        var (carId, marcaId, modeloId) = await SeedAsync(db, dealerId, StatusServiceCar.Disponible, completedSale: false);

        await using var ctx = new TestApplicationDbContext(Options(db), dealerId);
        var result = await Handler(ctx, marcaId, modeloId)
            .Handle(Command(carId, marcaId, modeloId, StatusServiceCar.Reservado), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await StatusOf(db, dealerId, carId)).Should().Be(StatusServiceCar.Reservado);
    }
}
