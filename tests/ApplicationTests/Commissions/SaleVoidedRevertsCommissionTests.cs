using Application.Commissions;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Commissions;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests.Commissions;

/// <summary>
/// sfc B4.1.2 — closes the B1.2.2 placeholder: <see cref="SaleVoidedDomainEvent"/> voids the sale's
/// commission. Not to be confused with sfc B7.1 consignment commission (a different concept,
/// handled by a different handler reacting to a different event's side effect).
/// </summary>
public class SaleVoidedRevertsCommissionTests
{
    private static readonly Guid DealerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);

    private static Car NewCar(TestApplicationDbContext context, string plate)
    {
        var marca = new Marca("Ford " + plate);
        var modelo = new Modelo("Focus", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1600, 50000, 2019, plate, "desc", 1_000_000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    [Fact]
    public async Task SaleVoided_VoidsTheExistingCommission()
    {
        using var context = CreateContext();
        Guid salespersonId = Guid.NewGuid();
        var buyer = new Client(DealerId, "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", DateTime.UtcNow);
        context.Clients.Add(buyer);
        var car = NewCar(context, "VOD010");
        await context.SaveChangesAsync();

        var sale = new Sale(DealerId, car.Id, buyer.Id, 1_000_000m, PaymentMethod.Cash, "C-VOD1", "c", DateTime.UtcNow,
            salespersonId: salespersonId);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var commission = SaleCommission.Create(
            DealerId, sale.Id, salespersonId,
            CommissionCalculator.Calculate(Money.Of(1_000_000m, CurrencyCode.Ars), null, CommissionRule.Percentage(3m)).Value,
            DateTime.UtcNow);
        context.SaleCommissions.Add(commission);
        await context.SaveChangesAsync();

        await new VoidSaleCommissionOnSaleVoidedHandler(context, NullLogger<VoidSaleCommissionOnSaleVoidedHandler>.Instance)
            .Handle(new SaleVoidedDomainEvent(sale.Id, car.Id, "Error de carga", Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);

        (await context.SaleCommissions.SingleAsync(c => c.SaleId == sale.Id)).Status.Should().Be(SaleCommissionStatus.Voided);
    }

    [Fact]
    public async Task SaleVoided_WithNoCommissionRow_IsASafeNoOp()
    {
        using var context = CreateContext();
        var buyer = new Client(DealerId, "Ana", "Perez", "30111222", "ana@x.com", "1", "Addr", DateTime.UtcNow);
        context.Clients.Add(buyer);
        var car = NewCar(context, "VOD020");
        await context.SaveChangesAsync();

        var sale = new Sale(DealerId, car.Id, buyer.Id, 1_000_000m, PaymentMethod.Cash, "C-VOD2", "c", DateTime.UtcNow);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        // No SaleCommission row for this sale (completed before the feature shipped, or never configured).
        Func<Task> act = () => new VoidSaleCommissionOnSaleVoidedHandler(
                context, NullLogger<VoidSaleCommissionOnSaleVoidedHandler>.Instance)
            .Handle(new SaleVoidedDomainEvent(sale.Id, car.Id, "Error de carga", Guid.NewGuid(), DateTime.UtcNow), CancellationToken.None);

        await act.Should().NotThrowAsync();
        (await context.SaleCommissions.AnyAsync(c => c.SaleId == sale.Id)).Should().BeFalse();
    }
}
