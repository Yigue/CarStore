using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Sales.Create;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Sales;

public class CreateSaleCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_Should_CreateSale_WhenDataIsValid()
    {
        using var context = CreateContext();
        var marca = new Marca("Ford");
        var modelo = new Modelo("Fiesta", marca.Id);
        var car = new Car(marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, statusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "XYZ789", "desc", 10000m, DateTime.UtcNow);
        var client = new Client("Alice", "Johnson", "789", "alice@test.com", "111", "Addr1");
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var handler = new CreateSaleCommandHandler(context);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN123", "ok");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Sales.Should().ContainSingle(s => s.Id == result.Value);
        (await context.Cars.FindAsync(car.Id))!.ServiceCar.Should().Be(statusServiceCar.Vendido);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Car_NotFound()
    {
        using var context = CreateContext();
        var client = new Client("Bob", "Brown", "123", "bob@test.com", "111", "Addr");
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var handler = new CreateSaleCommandHandler(context);
        var carId = Guid.NewGuid();
        var command = new CreateSaleCommand(carId, client.Id, 1000m, PaymentMethod.Cash, "C", "comment");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.NotFound(carId));
        context.Sales.Should().BeEmpty();
    }
}
