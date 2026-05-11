using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Sales.GetSummary;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Sales;

public class GetSalesSummaryQueryHandlerTests
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
        var car4 = new Car(dealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2023, "DDD444", "Car 4", 18000m, now);
        context.Cars.AddRange(car1, car2, car3, car4);
        await context.SaveChangesAsync();

        // Create sales - 2 completed ($20000 + $15000 = $35000), 1 pending ($25000), 1 cancelled ($18000)
        context.Sales.Add(new Sale(dealerId, car1.Id, client1.Id, 20000m, PaymentMethod.Cash, "S1", "", now));
        context.Sales.Add(new Sale(dealerId, car2.Id, client1.Id, 15000m, PaymentMethod.CreditCard, "S2", "", now));
        var pendingSale = new Sale(dealerId, car3.Id, client2.Id, 25000m, PaymentMethod.BankTransfer, "S3", "", now);
        // Keep pending - default state
        context.Sales.Add(pendingSale);
        var cancelledSale = new Sale(dealerId, car4.Id, client2.Id, 18000m, PaymentMethod.Cash, "S4", "", now);
        cancelledSale.Cancel("Customer request"); // Cancelled
        context.Sales.Add(cancelledSale);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ComputesTotalAmount()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetSalesSummaryQueryHandler(context);
        var query = new GetSalesSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(78000m); // 20000 + 15000 + 25000 + 18000
    }

    [Fact]
    public async Task Handle_ComputesAveragePrice()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetSalesSummaryQueryHandler(context);
        var query = new GetSalesSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(4);
        result.Value.AveragePrice.Should().Be(19500m); // 78000 / 4
    }

    [Fact]
    public async Task Handle_ByStatusCountsCorrect()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetSalesSummaryQueryHandler(context);
        var query = new GetSalesSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ByStatus.Pending.Should().Be(1);
        result.Value.ByStatus.Completed.Should().Be(2);
        result.Value.ByStatus.Cancelled.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ReturnsZeros_WhenNoSales()
    {
        using var context = CreateContext();
        var handler = new GetSalesSummaryQueryHandler(context);
        var query = new GetSalesSummaryQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAmount.Should().Be(0m);
        result.Value.TotalCount.Should().Be(0);
        result.Value.AveragePrice.Should().Be(0m);
    }
}