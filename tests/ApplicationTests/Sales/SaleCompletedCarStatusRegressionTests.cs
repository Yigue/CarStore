using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Sales.Update;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Sales;

/// <summary>
/// F-DEALER-OPS (H5, H13 — spr PR 7.3.2). Regression guard for the rule the dashboard now relies on:
/// a car becomes <c>Vendido</c> because its sale was completed, never because someone picked
/// "Vendido" in a form. The frontend removed the manual option (bulk actions and vehicle form); if
/// this stops holding, there is no other way left for a sold car to leave the public catalogue.
/// </summary>
public class SaleCompletedCarStatusRegressionTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UpdateSaleCommandHandler CreateHandler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new UpdateSaleCommandHandler(context, new FakeDateTimeProvider(), tenant.Object);
    }

    private static Car SeedCar(TestApplicationDbContext context, StatusServiceCar status)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.Used,
            status, 4, 5, 1600, 45000, 2020, "AB123CD", "desc", 10000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    private static Sale SeedPendingSale(TestApplicationDbContext context, Guid carId)
    {
        var sale = new Sale(DealerId, carId, Guid.NewGuid(), 10000m, PaymentMethod.Cash, "CN-1", "initial", DateTime.UtcNow);
        context.Sales.Add(sale);
        return sale;
    }

    [Theory]
    [InlineData(StatusServiceCar.Reservado)]
    [InlineData(StatusServiceCar.Disponible)]
    [InlineData(StatusServiceCar.EnVenta)]
    public async Task Completing_a_pending_sale_marks_the_car_as_sold_without_manual_intervention(StatusServiceCar initial)
    {
        using var context = CreateContext();
        var car = SeedCar(context, initial);
        var sale = SeedPendingSale(context, car.Id);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new UpdateSaleCommand(sale.Id, 10000m, PaymentMethod.Cash, SaleStatus.Completed, "CN-1", "cierre"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persistedSale = await context.Sales.FirstAsync(s => s.Id == sale.Id);
        var persistedCar = await context.Cars.FirstAsync(c => c.Id == car.Id);
        persistedSale.Status.Should().Be(SaleStatus.Completed);
        persistedCar.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }

    [Fact]
    public async Task Editing_a_sale_that_stays_pending_does_not_mark_the_car_as_sold()
    {
        using var context = CreateContext();
        var car = SeedCar(context, StatusServiceCar.Reservado);
        var sale = SeedPendingSale(context, car.Id);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(
            new UpdateSaleCommand(sale.Id, 9500m, PaymentMethod.Cash, SaleStatus.Pending, "CN-1", "rebaja"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.Cars.FirstAsync(c => c.Id == car.Id)).ServiceCar.Should().NotBe(StatusServiceCar.Vendido);
    }
}
