using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Authentication;
using Application.Abstractions.Storage;
using Application.Cars.Get;
using Application.Cars.GetByIds;
using Domain.Cars;
using Domain.Cars.Attributes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Cars;

public class GetCarsByIdsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_Should_ReturnPatente_OnlyToAuthenticatedCallers(bool isAuthenticated)
    {
        // Arrange
        using var context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);

        var car = new Car(
            Guid.NewGuid(), marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.Used,
            StatusServiceCar.Disponible, 4, 5, 1600, 10_000, 2022, "PAT123", "Test car",
            25000m, DateTime.UtcNow, FuelType.Gasolina, false, Transmission.Manual, null);

        context.Cars.Add(car);
        await context.SaveChangesAsync();

        var userContext = new Mock<IUserContext>();
        userContext.Setup(x => x.IsAuthenticated).Returns(isAuthenticated);

        var mockStorage = new Mock<IStorageService>();
        var handler = new GetCarsByIdsQueryHandler(context, mockStorage.Object, userContext.Object);

        // Act
        var result = await handler.Handle(new GetCarsByIdsQuery([car.Id]), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        if (isAuthenticated)
        {
            result.Value[0].Patente.Should().Be("PAT123");
        }
        else
        {
            result.Value[0].Patente.Should().BeNull();
        }
    }
}
