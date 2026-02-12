using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Cars.Create;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Microsoft.EntityFrameworkCore;
using Moq;
using Application.Abstractions.Caching;

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
            .ReturnsAsync(marca);

        var mockModelService = new Mock<ICachedModelService>();
        mockModelService.Setup(s => s.GetByIdAsync(modelo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(modelo);

        var handler = new CreateCarCommandHandler(context, dateProvider, mockBrandService.Object, mockModelService.Object);

        var command = new CreateCarCommand
        {
            Marca = marca.Id,
            Modelo = modelo.Id,
            Color = Color.Blue,
            CarType = TypeCar.Sedan,
            CarStatus = StatusCar.New,
            ServiceCar = statusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 0,
            Año = 2024,
            Patente = "ABC123",
            Descripcion = "New car",
            Price = 20000m
        };

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
            .ReturnsAsync((Marca?)null); // Brand not found

        var mockModelService = new Mock<ICachedModelService>();

        var handler = new CreateCarCommandHandler(context, dateProvider, mockBrandService.Object, mockModelService.Object);

        var command = new CreateCarCommand
        {
            Marca = Guid.NewGuid(),
            Modelo = Guid.NewGuid(),
            Color = Color.Black,
            CarType = TypeCar.Sedan,
            CarStatus = StatusCar.New,
            ServiceCar = statusServiceCar.Disponible,
            CantidadPuertas = 4,
            CantidadAsientos = 5,
            Cilindrada = 2000,
            Kilometraje = 0,
            Año = 2024,
            Patente = "DEF456",
            Descripcion = "Invalid car",
            Price = 15000m
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.AtributesInvalid());
        context.Cars.Should().BeEmpty();
    }
}
