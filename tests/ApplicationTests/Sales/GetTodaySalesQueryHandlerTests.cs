using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Sales.GetToday;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Sales;

public class GetTodaySalesQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task SeedTestDataAsync(TestApplicationDbContext context)
    {
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Create brand and model
        var marca = new Marca("Toyota");
        context.Marca.Add(marca);
        await context.SaveChangesAsync();

        var modelo = new Modelo("Corolla", marca.Id);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        // Create clients
        var client1 = new Client(dealerId, "Client", "One", "111", "c1@test.com", "111", "Addr1", now);
        var client2 = new Client(dealerId, "Client", "Two", "222", "c2@test.com", "222", "Addr2", now);
        context.Clients.AddRange(client1, client2);
        await context.SaveChangesAsync();

        // Create cars
        var car1 = new Car(dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2020, "AAA111", "Car 1", 20000m, now);
        var car2 = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2021, "BBB222", "Car 2", 15000m, now);
        var car3 = new Car(dealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2022, "CCC333", "Car 3", 25000m, now);
        context.Cars.AddRange(car1, car2, car3);
        await context.SaveChangesAsync();

        // Create sales - 2 today, 1 yesterday
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        context.Sales.Add(new Sale(dealerId, car1.Id, client1.Id, 20000m, PaymentMethod.Cash, "S1", "", today.AddHours(9)));
        context.Sales.Add(new Sale(dealerId, car2.Id, client1.Id, 15000m, PaymentMethod.CreditCard, "S2", "", today.AddHours(14)));
        context.Sales.Add(new Sale(dealerId, car3.Id, client2.Id, 25000m, PaymentMethod.BankTransfer, "S3", "", yesterday));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_FiltersTodaySalesOnly()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetTodaySalesQueryHandler(context);
        var query = new GetTodaySalesQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Sales.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ComputesDailyTotal()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetTodaySalesQueryHandler(context);
        var query = new GetTodaySalesQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(35000m); // 20000 + 15000
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoSalesToday()
    {
        using var context = CreateContext();

        // Add a sale from yesterday only
        var dealerId = Guid.NewGuid();
        var marca = new Marca("Toyota");
        context.Marca.Add(marca);
        await context.SaveChangesAsync();

        var modelo = new Modelo("Corolla", marca.Id);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        var client = new Client(dealerId, "Client", "One", "111", "c1@test.com", "111", "Addr1", DateTime.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync();

        var car = new Car(dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2020, "AAA111", "Car 1", 20000m, DateTime.UtcNow);
        context.Cars.Add(car);
        await context.SaveChangesAsync();

        // Sale from yesterday
        context.Sales.Add(new Sale(dealerId, car.Id, client.Id, 20000m, PaymentMethod.Cash, "S1", "", DateTime.UtcNow.AddDays(-1)));
        await context.SaveChangesAsync();

        var handler = new GetTodaySalesQueryHandler(context);
        var query = new GetTodaySalesQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(0);
        result.Value.TotalAmount.Should().Be(0m);
        result.Value.Sales.Should().BeEmpty();
    }
}