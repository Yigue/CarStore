using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Cars.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;
using Application.Abstractions.Caching;
using Application.Abstractions.Tenancy;

namespace Application.UnitTests.Cars;

public class CreateCarCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_CreateCar_WhenAttributesAreValid()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        var mockBrandService = new Mock<ICachedBrandService>();
        mockBrandService.Setup(s => s.GetByIdAsync(marca.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre });

        var mockModelService = new Mock<ICachedModelService>();
        mockModelService.Setup(s => s.GetByIdAsync(modelo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.ModeloCacheDto { Id = modelo.Id, Nombre = modelo.Nombre, MarcaId = modelo.MarcaId });

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.DealerId).Returns(Guid.NewGuid());

        var handler = new CreateCarCommandHandler(context, dateProvider, mockBrandService.Object, mockModelService.Object, mockTenantService.Object);

        var command = new CreateCarCommand(
            marca.Id,
            modelo.Id,
            Color.Blue,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4,
            5,
            2000,
            0,
            2024,
            "ABC123",
            "New car",
            20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Cars.Should().ContainSingle(c => c.Id == result.Value);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Marca_NotFound()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider();
        
        var mockBrandService = new Mock<ICachedBrandService>();
        mockBrandService.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Application.Abstractions.Caching.MarcaCacheDto?)null); // Brand not found

        var mockModelService = new Mock<ICachedModelService>();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.DealerId).Returns(Guid.NewGuid());

        var handler = new CreateCarCommandHandler(context, dateProvider, mockBrandService.Object, mockModelService.Object, mockTenantService.Object);

        var command = new CreateCarCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Color.Black,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4,
            5,
            2000,
            0,
            2024,
            "DEF456",
            "Invalid car",
            15000m);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.AtributesInvalid());
        context.Cars.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_CreateSecondCar_WhenCacheReturnsDetachedGraph()
    {
        using var context = CreateContext();
        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        // Simulate a warm cache where the service returns detached instances (new objects with same IDs)
        var cachedMarca = Marca.WithId(marca.Id, "Toyota");
        var cachedModelo = Modelo.WithId(modelo.Id, "Corolla", marca.Id);
        cachedModelo.Marca = cachedMarca;

        var mockBrandService = new Mock<ICachedBrandService>();
        mockBrandService.Setup(s => s.GetByIdAsync(marca.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.MarcaCacheDto { Id = cachedMarca.Id, Nombre = cachedMarca.Nombre });

        var mockModelService = new Mock<ICachedModelService>();
        mockModelService.Setup(s => s.GetByIdAsync(modelo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.ModeloCacheDto { Id = cachedModelo.Id, Nombre = cachedModelo.Nombre, MarcaId = cachedModelo.MarcaId });

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.DealerId).Returns(Guid.NewGuid());

        // Pre-attach the original marca to force identity collision if the handler attaches the detached graph
        context.Marca.Attach(marca);

        var handler = new CreateCarCommandHandler(context, dateProvider, mockBrandService.Object, mockModelService.Object, mockTenantService.Object);

        var command = new CreateCarCommand(
            marca.Id,
            modelo.Id,
            Color.Blue,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Disponible,
            4,
            5,
            2000,
            0,
            2024,
            "XYZ789",
            "Second car",
            25000m);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Cars.Should().ContainSingle(c => c.Id == result.Value);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_WhenSamePatenteExistsInSameDealer()
    {
        var dealerId = Guid.NewGuid();
        using var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            dealerId);

        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        var existingCar = new Car(dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Disponible, 4, 5, 2000, 0, 2024, "ABC123", "existing", 20000m, dateProvider.UtcNow);
        context.Cars.Add(existingCar);
        await context.SaveChangesAsync();

        var mockBrandService = new Mock<ICachedBrandService>();
        mockBrandService.Setup(s => s.GetByIdAsync(marca.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.MarcaCacheDto { Id = marca.Id, Nombre = marca.Nombre });

        var mockModelService = new Mock<ICachedModelService>();
        mockModelService.Setup(s => s.GetByIdAsync(modelo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.ModeloCacheDto { Id = modelo.Id, Nombre = modelo.Nombre, MarcaId = modelo.MarcaId });

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.DealerId).Returns(dealerId);
        mockTenantService.Setup(t => t.HasTenant).Returns(true);

        var handler = new CreateCarCommandHandler(context, dateProvider, mockBrandService.Object, mockModelService.Object, mockTenantService.Object);

        var command = new CreateCarCommand(
            marca.Id, modelo.Id, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 2000, 0, 2024, "ABC123", "duplicate in same dealer", 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.PatenteAlreadyExists("ABC123"));
    }

    [Fact]
    public async Task Handle_Should_Succeed_WhenSamePatenteExistsInDifferentDealer()
    {
        var dealerA = Guid.NewGuid();
        var dealerB = Guid.NewGuid();
        string dbName = Guid.NewGuid().ToString();

        var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2024, 1, 1) };
        Guid marcaId, modeloId;

        // Seed car in dealerA
        await using (var seedCtx = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options,
            dealerA))
        {
            var marca = new Marca("Toyota");
            var modelo = new Modelo("Corolla", marca.Id);
            seedCtx.Marca.Add(marca);
            seedCtx.Modelo.Add(modelo);
            var carInDealerA = new Car(dealerA, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New,
                StatusServiceCar.Disponible, 4, 5, 2000, 0, 2024, "ABC123", "car in dealer A", 20000m, dateProvider.UtcNow);
            seedCtx.Cars.Add(carInDealerA);
            await seedCtx.SaveChangesAsync();
            marcaId = marca.Id;
            modeloId = modelo.Id;
        }

        // DealerB creates car with same patente "ABC123"
        await using var context = new TestApplicationDbContext(
            new DbContextOptionsBuilder<TestApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options,
            dealerB);

        var mockBrandService = new Mock<ICachedBrandService>();
        mockBrandService.Setup(s => s.GetByIdAsync(marcaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.MarcaCacheDto { Id = marcaId, Nombre = "Toyota" });

        var mockModelService = new Mock<ICachedModelService>();
        mockModelService.Setup(s => s.GetByIdAsync(modeloId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Abstractions.Caching.ModeloCacheDto { Id = modeloId, Nombre = "Corolla", MarcaId = marcaId });

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(t => t.DealerId).Returns(dealerB);
        mockTenantService.Setup(t => t.HasTenant).Returns(true);

        var handler = new CreateCarCommandHandler(context, dateProvider, mockBrandService.Object, mockModelService.Object, mockTenantService.Object);

        var command = new CreateCarCommand(
            marcaId, modeloId, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 2000, 0, 2024, "ABC123", "car in dealer B with same patente", 20000m);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(
            "Different dealers are isolated and can use the same patente without cross-tenant conflict");
    }
}
