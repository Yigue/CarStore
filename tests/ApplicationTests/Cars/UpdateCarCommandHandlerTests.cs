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
            .ReturnsAsync(new Application.Abstractions.Caching.MarcaCacheDto { Id = detachedMarca.Id, Nombre = detachedMarca.Nombre });

        var modelService = new Mock<ICachedModelService>();
        modelService.Setup(s => s.GetByIdAsync(modeloId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.ModeloCacheDto { Id = detachedModelo.Id, Nombre = detachedModelo.Nombre, MarcaId = detachedModelo.MarcaId });

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

    // ── INV-01: the cover is part of what an update can change ───────────────────────────────
    //
    // Until now the only route to it was PATCH cars/{id}/images/{imageId}/cover, which the edit
    // form never called — so "editar el vehículo" could change every field except the one the
    // buyer sees first.

    /// <summary>Seeds a car with three images, the first of them the cover.</summary>
    private static async Task<(Guid carId, Guid marcaId, Guid modeloId, Guid[] imageIds)> SeedCarWithImagesAsync(
        string db,
        Guid dealerId)
    {
        await using var seed = new TestApplicationDbContext(Options(db), dealerId);

        var marca = new Marca("Peugeot");
        var modelo = new Modelo("208", marca.Id);
        seed.Marca.Add(marca);
        seed.Modelo.Add(modelo);
        await seed.SaveChangesAsync();

        var car = new Car(dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 1600, 0, 2021, "COV111", "seed", 20000m, DateTime.UtcNow);
        seed.Cars.Add(car);
        await seed.SaveChangesAsync();

        var images = new[]
        {
            new CarImage(car.Id, "https://cdn/1.jpg", isCover: true, displayOrder: 0),
            new CarImage(car.Id, "https://cdn/2.jpg", isCover: false, displayOrder: 1),
            new CarImage(car.Id, "https://cdn/3.jpg", isCover: false, displayOrder: 2),
        };
        seed.CarImages.AddRange(images);
        await seed.SaveChangesAsync();

        return (car.Id, marca.Id, modelo.Id, images.Select(i => i.Id).ToArray());
    }

    private static UpdateCarCommandHandler CreateHandler(
        TestApplicationDbContext ctx,
        Guid marcaId,
        Guid modeloId,
        string marcaName,
        string modeloName)
    {
        var brandService = new Mock<ICachedBrandService>();
        brandService.Setup(s => s.GetByIdAsync(marcaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarcaCacheDto { Id = marcaId, Nombre = marcaName });

        var modelService = new Mock<ICachedModelService>();
        modelService.Setup(s => s.GetByIdAsync(modeloId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ModeloCacheDto { Id = modeloId, Nombre = modeloName, MarcaId = marcaId });

        return new UpdateCarCommandHandler(
            ctx,
            new FakeDateTimeProvider { UtcNow = new DateTime(2024, 1, 1) },
            brandService.Object,
            modelService.Object);
    }

    private static UpdateCarCommand BuildUpdate(Guid carId, Guid marcaId, Guid modeloId, Guid? coverImageId) =>
        new(carId, marcaId, modeloId, Color.Red, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 100, 2021, "COV111", "updated",
            20000m, FuelType.Gasolina, false, Transmission.Manual, null, coverImageId);

    [Fact]
    public async Task Handle_Should_ReassignTheCover_When_CoverImageIdIsSupplied()
    {
        string db = Guid.NewGuid().ToString();
        var dealerId = Guid.NewGuid();
        var (carId, marcaId, modeloId, imageIds) = await SeedCarWithImagesAsync(db, dealerId);

        await using (var ctx = new TestApplicationDbContext(Options(db), dealerId))
        {
            var handler = CreateHandler(ctx, marcaId, modeloId, "Peugeot", "208");
            var result = await handler.Handle(
                BuildUpdate(carId, marcaId, modeloId, imageIds[2]),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        await using var verify = new TestApplicationDbContext(Options(db), dealerId);
        var images = await verify.CarImages.Where(i => i.CarId == carId).ToListAsync();

        images.Single(i => i.Id == imageIds[2]).IsCover.Should().BeTrue();
        images.Count(i => i.IsCover).Should().Be(1, "exactly one cover per car");
    }

    [Fact]
    public async Task Handle_Should_LeaveTheGalleryAlone_When_CoverImageIdIsNull()
    {
        string db = Guid.NewGuid().ToString();
        var dealerId = Guid.NewGuid();
        var (carId, marcaId, modeloId, imageIds) = await SeedCarWithImagesAsync(db, dealerId);

        await using (var ctx = new TestApplicationDbContext(Options(db), dealerId))
        {
            var handler = CreateHandler(ctx, marcaId, modeloId, "Peugeot", "208");
            await handler.Handle(BuildUpdate(carId, marcaId, modeloId, null), CancellationToken.None);
        }

        await using var verify = new TestApplicationDbContext(Options(db), dealerId);
        var images = await verify.CarImages.Where(i => i.CarId == carId).ToListAsync();

        images.Single(i => i.Id == imageIds[0]).IsCover.Should().BeTrue(
            "an update that says nothing about images must not disturb them");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_TheCoverImageBelongsToAnotherCar()
    {
        string db = Guid.NewGuid().ToString();
        var dealerId = Guid.NewGuid();
        var (carId, marcaId, modeloId, imageIds) = await SeedCarWithImagesAsync(db, dealerId);

        await using var ctx = new TestApplicationDbContext(Options(db), dealerId);
        var handler = CreateHandler(ctx, marcaId, modeloId, "Peugeot", "208");

        var result = await handler.Handle(
            BuildUpdate(carId, marcaId, modeloId, Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Image.NotFoundInCar");

        // And the update is refused whole: nothing is written when the cover cannot be applied.
        await using var verify = new TestApplicationDbContext(Options(db), dealerId);
        var images = await verify.CarImages.Where(i => i.CarId == carId).ToListAsync();
        images.Single(i => i.Id == imageIds[0]).IsCover.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_BeIdempotent_When_TheCoverIsAlreadyTheRequestedImage()
    {
        string db = Guid.NewGuid().ToString();
        var dealerId = Guid.NewGuid();
        var (carId, marcaId, modeloId, imageIds) = await SeedCarWithImagesAsync(db, dealerId);

        await using (var ctx = new TestApplicationDbContext(Options(db), dealerId))
        {
            var handler = CreateHandler(ctx, marcaId, modeloId, "Peugeot", "208");
            var result = await handler.Handle(
                BuildUpdate(carId, marcaId, modeloId, imageIds[0]),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        await using var verify = new TestApplicationDbContext(Options(db), dealerId);
        var images = await verify.CarImages.Where(i => i.CarId == carId).ToListAsync();
        images.Count(i => i.IsCover).Should().Be(1);
        images.Single(i => i.Id == imageIds[0]).IsCover.Should().BeTrue();
    }
}
