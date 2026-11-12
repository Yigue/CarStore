using Application.Abstractions.Caching;
using Application.Queries.Cars.GetInventoryStats;
using Application.Sales.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Sales;

/// <summary>
/// sfc B7.1 / RN-036 scenarios: a consigned vehicle sold for ARS 2.000.000 at 10% records ARS 200.000 of income, not
/// the whole price; and it is left out of the dealer's own inventory value.
/// </summary>
public class ConsignmentIncomeTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);

    private static Car NewCar(TestApplicationDbContext context, string plate, decimal price)
    {
        var marca = new Marca("Toyota " + plate);
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1800, 40000, 2020, plate, "desc", price, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    [Fact]
    public async Task Completing_the_sale_of_a_consigned_car_records_only_the_commission()
    {
        using var context = CreateContext();
        context.DealerSettings.Add(new Domain.DealerSettings.DealerSettings(DealerId, "Test", "t@t.com"));
        var car = NewCar(context, "CSG010", 2_000_000m);
        var owner = new Client(DealerId, "Dueño", "Tercero", "20111222", "owner@x.com", "1", "Addr", DateTime.UtcNow);
        var buyer = new Client(DealerId, "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", DateTime.UtcNow);
        context.Clients.AddRange(owner, buyer);
        await context.SaveChangesAsync();
        car.PlaceOnConsignment(owner.Id, 0.10m);
        var sale = new Sale(DealerId, car.Id, buyer.Id, 2_000_000m, PaymentMethod.Cash, "C-CSG", "c", DateTime.UtcNow);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        await new SaleCompletedDomainEventHandler(context, new FakeDateTimeProvider(), new Mock<ICachedCategoryService>().Object)
            .Handle(new SaleCompletedDomainEvent(sale.Id, car.Id, buyer.Id, Money.Of(2_000_000m, CurrencyCode.Ars), PaymentMethod.Cash),
                CancellationToken.None);

        (await context.Transactions.SingleAsync()).Amount.Should().Be(Money.Of(200_000m, CurrencyCode.Ars));
    }

    [Fact]
    public async Task A_consigned_car_is_left_out_of_the_own_inventory_value()
    {
        using var context = CreateContext();
        NewCar(context, "CSG020", 1_000m);
        var consigned = NewCar(context, "CSG021", 5_000m);
        consigned.PlaceOnConsignment(Guid.NewGuid(), 0.1m);
        await context.SaveChangesAsync();

        var result = await new GetInventoryStatsQueryHandler(context).Handle(new GetInventoryStatsQuery(), CancellationToken.None);

        result.Value.TotalValue.Should().Be(1_000m);
    }
}
