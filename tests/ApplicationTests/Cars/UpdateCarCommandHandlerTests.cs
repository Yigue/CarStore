using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Caching;
using Application.Cars.Update;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Cars;

public class UpdateCarCommandHandlerTests
{
    private static DbContextOptions<TestApplicationDbContext> Options(string dbName) =>
        new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

    /// <summary>
    /// Regression: the cached brand/model services round-trip entities through Redis, so on a cache
    /// HIT they return DETACHED instances that each carry their own Marca object. Attaching those
    /// duplicate Marca instances (same Id) to the tracked car made EF throw
    /// "The instance of entity type 'Marca' cannot be tracked because another instance with the same
    /// key value is already being tracked" → HTTP 500 on every vehicle update once the cache warmed up.
    /// The handler must attach the context-tracked single instance instead.
    /// </summary>
    [Fact]
    public async Task Handle_Should_Update_When_CacheReturnsDetachedDuplicateMarcaInstances()
    {
        string db = Guid.NewGuid().ToString();
        var dealerId = Guid.NewGuid();

        Guid carId, marcaId, modeloId;

        // Seed marca + modelo + car in the shared in-memory store.
        await using (var seed = new TestApplicationDbContext(Options(db), dealerId))
        {
            var marca = new Marca("Toyota");
            var modelo = new Modelo("Corolla", marca.Id);
            seed.Marca.Add(marca);
            seed.Modelo.Add(modelo);
            await seed.SaveChangesAsync();

            var car = new Car(dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New,
                StatusServiceCar.Disponible, 4, 5, 2000, 0, 2020, "ABC123", "seed", 25000m, DateTime.UtcNow);
            seed.Cars.Add(car);
            await seed.SaveChangesAsync();

            carId = car.Id;
            marcaId = marca.Id;
            modeloId = modelo.Id;
        }

        // Two SEPARATE contexts produce detached instances with the same Ids, mimicking what the
        // Redis-backed cache returns on a hit: a Marca instance from the brand cache and a different
        // Marca instance hanging off the model's navigation.
        Marca detachedMarca;
        await using (var brandCtx = new TestApplicationDbContext(Options(db), dealerId))
        {
            detachedMarca = await brandCtx.Marca.AsNoTracking().FirstAsync(m => m.Id == marcaId);
        }

        Modelo detachedModelo;
        await using (var modelCtx = new TestApplicationDbContext(Options(db), dealerId))
        {
            detachedModelo = await modelCtx.Modelo.AsNoTracking()
                .Include(m => m.Marca)
                .FirstAsync(m => m.Id == modeloId);
        }

        var brandService = new Mock<ICachedBrandService>();
        brandService.Setup(s => s.GetByIdAsync(marcaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detachedMarca);

        var modelService = new Mock<ICachedModelService>();
        modelService.Setup(s => s.GetByIdAsync(modeloId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detachedModelo);

        await using var ctx = new TestApplicationDbContext(Options(db), dealerId);
        var handler = new UpdateCarCommandHandler(
            ctx,
            new FakeDateTimeProvider { UtcNow = new DateTime(2024, 1, 1) },
            brandService.Object,
            modelService.Object);

        var command = new UpdateCarCommand(
            carId, marcaId, modeloId, Color.Red, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1800, 12345, 2022, "XYZ789", "updated",
            30000m, FuelType.Gasolina, false, Transmission.Manual, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await using var verify = new TestApplicationDbContext(Options(db), dealerId);
        var updated = await verify.Cars.FirstAsync(c => c.Id == carId);
        updated.Anio.Should().Be(2022);
        updated.Kilometraje.Should().Be(12345);
    }
}
