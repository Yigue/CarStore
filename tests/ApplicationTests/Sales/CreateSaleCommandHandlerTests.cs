using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Abstractions.Tenancy;
using Application.Sales.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Moq;

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
        var dealerId = Guid.NewGuid();
        var car = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible, 4, 5, 1600, 1000, 2020, "XYZ789", "desc", 10000m, DateTime.UtcNow);
        var client = new Client(dealerId, "Alice", "Johnson", "789", "alice@test.com", "111", "Addr1", DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var command = new CreateSaleCommand(car.Id, client.Id, 9000m, PaymentMethod.Cash, "CN123", "ok");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Sales.Should().ContainSingle(s => s.Id == result.Value);
        (await context.Cars.FindAsync(car.Id))!.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_Car_NotFound()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, "Bob", "Brown", "123", "bob@test.com", "111", "Addr", DateTime.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var dateProvider = new FakeDateTimeProvider();
        var tenantService = new Mock<ICurrentTenantService>();
        tenantService.Setup(t => t.DealerId).Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var handler = new CreateSaleCommandHandler(context, dateProvider, tenantService.Object);
        var carId = Guid.NewGuid();
        var command = new CreateSaleCommand(carId, client.Id, 1000m, PaymentMethod.Cash, "C", "comment");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.NotFound(carId));
        context.Sales.Should().BeEmpty();
    }
}
