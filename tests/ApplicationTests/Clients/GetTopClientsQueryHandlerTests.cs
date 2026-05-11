using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Clients.GetTop;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Moq;
using Application.Abstractions.Tenancy;

namespace Application.UnitTests.Clients;

public class GetTopClientsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task SeedTestDataWithSalesAsync(TestApplicationDbContext context)
    {
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Create brands and models
        var marca = new Marca("Toyota");
        context.Marca.Add(marca);
        await context.SaveChangesAsync();

        var modelo = new Modelo("Corolla", marca.Id);
        context.Modelo.Add(modelo);
        await context.SaveChangesAsync();

        // Create clients with different revenue profiles
        var client1 = new Client(dealerId, "High", "Spender", "111", "high@test.com", "111", "Addr1", now);
        var client2 = new Client(dealerId, "Medium", "Spender", "222", "medium@test.com", "222", "Addr2", now);
        var client3 = new Client(dealerId, "Low", "Spender", "333", "low@test.com", "333", "Addr3", now);
        var client4 = new Client(dealerId, "No", "Spender", "444", "no@test.com", "444", "Addr4", now);

        context.Clients.AddRange(client1, client2, client3, client4);
        await context.SaveChangesAsync();

        // Create cars
        var car1 = new Car(dealerId, marca, modelo, Color.Blue, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2020, "AAA111", "Car 1", 30000m, now);
        var car2 = new Car(dealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2021, "BBB222", "Car 2", 20000m, now);
        var car3 = new Car(dealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2022, "CCC333", "Car 3", 10000m, now);
        var car4 = new Car(dealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 0, 2023, "DDD444", "Car 4", 15000m, now);

        context.Cars.AddRange(car1, car2, car3, car4);
        await context.SaveChangesAsync();

        // Create sales with different amounts
        // Client1: $30000 + $20000 = $50000
        // Client2: $10000
        // Client3: $15000
        // Client4: no sales = $0

        // We need to use reflection or create a helper since Sale constructor is complex
        // For simplicity, directly add sales via EF
        context.Sales.Add(new Sale(dealerId, car1.Id, client1.Id, 30000m, PaymentMethod.Cash, "S1", "", now));
        context.Sales.Add(new Sale(dealerId, car2.Id, client1.Id, 20000m, PaymentMethod.Cash, "S2", "", now));
        context.Sales.Add(new Sale(dealerId, car3.Id, client2.Id, 10000m, PaymentMethod.CreditCard, "S3", "", now));
        context.Sales.Add(new Sale(dealerId, car4.Id, client3.Id, 15000m, PaymentMethod.BankTransfer, "S4", "", now));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_OrdersByTotalSalesAmount()
    {
        using var context = CreateContext();
        await SeedTestDataWithSalesAsync(context);
        var handler = new GetTopClientsQueryHandler(context);
        var query = new GetTopClientsQuery { Limit = 10 };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var clients = result.Value.ToList();

        clients.Should().HaveCount(4);
        // HighSpender should be first with $50000
        clients[0].FirstName.Should().Be("High");
        clients[0].TotalSalesAmount.Should().Be(50000m);
        // LowSpender should be second with $15000
        clients[1].FirstName.Should().Be("Low");
        clients[1].TotalSalesAmount.Should().Be(15000m);
        // MediumSpender should be third with $10000
        clients[2].FirstName.Should().Be("Medium");
        clients[2].TotalSalesAmount.Should().Be(10000m);
        // NoSpender should be last with $0
        clients[3].FirstName.Should().Be("No");
        clients[3].TotalSalesAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_IncludesRevenueInResponse()
    {
        using var context = CreateContext();
        await SeedTestDataWithSalesAsync(context);
        var handler = new GetTopClientsQueryHandler(context);
        var query = new GetTopClientsQuery { Limit = 4 };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var clients = result.Value.ToList();

        foreach (var client in clients)
        {
            client.TotalSalesAmount.Should().BeGreaterOrEqualTo(0);
        }
    }

    [Fact]
    public async Task Handle_RespectsLimit()
    {
        using var context = CreateContext();
        await SeedTestDataWithSalesAsync(context);
        var handler = new GetTopClientsQueryHandler(context);
        var query = new GetTopClientsQuery { Limit = 2 };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ClientsWithNoSalesHaveZeroRevenue()
    {
        using var context = CreateContext();
        await SeedTestDataWithSalesAsync(context);
        var handler = new GetTopClientsQueryHandler(context);
        var query = new GetTopClientsQuery { Limit = 4 };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var noSpender = result.Value.First(c => c.FirstName == "No");
        noSpender.TotalSalesAmount.Should().Be(0m);
    }
}