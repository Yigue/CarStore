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
/// sfc B4.1.4 — <see cref="CalculateSaleCommissionHandler"/> reacting to
/// <see cref="SaleCompletedDomainEvent"/> (RN-008, RN-009, D-COMMISSION).
/// </summary>
public class CalculateSaleCommissionHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, DealerId);

    private static Car NewCar(TestApplicationDbContext context, string plate, decimal price)
    {
        var marca = new Marca("Chevrolet " + plate);
        var modelo = new Modelo("Onix", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.White, TypeCar.Sedan, StatusCar.Used, StatusServiceCar.Disponible,
            4, 5, 1400, 30000, 2021, plate, "desc", price, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    private static async Task<(Sale Sale, Client Buyer)> SeedPendingSaleAsync(
        TestApplicationDbContext context, string plate, decimal price, decimal? tradeInValue = null, Guid? salespersonId = null)
    {
        var buyer = new Client(DealerId, "Ana", "Perez", Guid.NewGuid().ToString("N")[..8], "ana@x.com", "1", "Addr", DateTime.UtcNow);
        context.Clients.Add(buyer);
        var car = NewCar(context, plate, price);
        await context.SaveChangesAsync();

        var sale = new Sale(DealerId, car.Id, buyer.Id, price, PaymentMethod.Cash, "C-" + plate, "c", DateTime.UtcNow,
            salespersonId: salespersonId);
        if (tradeInValue is decimal trade)
        {
            sale.SetPaymentTerms(null, Guid.NewGuid(), trade, null, null, null, null);
        }

        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        return (sale, buyer);
    }

    private static CalculateSaleCommissionHandler NewHandler(TestApplicationDbContext context) =>
        new(context, new FakeDateTimeProvider(), NullLogger<CalculateSaleCommissionHandler>.Instance);

    [Fact]
    public async Task NoPolicyConfigured_CreatesZeroAmountCommissionFlaggedUnconfigured()
    {
        using var context = CreateContext();
        (Sale sale, _) = await SeedPendingSaleAsync(context, "CMS010", 1_000_000m);

        await NewHandler(context).Handle(
            new SaleCompletedDomainEvent(sale.Id, sale.CarId, sale.ClientId, sale.FinalPrice, PaymentMethod.Cash), CancellationToken.None);

        SaleCommission commission = await context.SaleCommissions.SingleAsync(c => c.SaleId == sale.Id);
        commission.Amount.Amount.Should().Be(0m);
        commission.IsConfigured.Should().BeFalse();
        commission.Status.Should().Be(SaleCommissionStatus.Pending);
    }

    [Fact]
    public async Task DealerDefaultRate_IsAppliedWhenNoSalespersonOverride()
    {
        using var context = CreateContext();
        Guid salespersonId = Guid.NewGuid();
        (Sale sale, _) = await SeedPendingSaleAsync(context, "CMS020", 1_000_000m, salespersonId: salespersonId);
        CommissionPolicy policy = CommissionPolicy.CreateDefault(DealerId);
        policy.SetDefaultRate(4m);
        context.CommissionPolicies.Add(policy);
        await context.SaveChangesAsync();

        await NewHandler(context).Handle(
            new SaleCompletedDomainEvent(sale.Id, sale.CarId, sale.ClientId, sale.FinalPrice, PaymentMethod.Cash), CancellationToken.None);

        SaleCommission commission = await context.SaleCommissions.SingleAsync(c => c.SaleId == sale.Id);
        commission.Rate.Should().Be(4m);
        commission.Amount.Amount.Should().Be(40_000m);
    }

    [Fact]
    public async Task SalespersonOverride_BeatsDealerDefault()
    {
        using var context = CreateContext();
        Guid salespersonId = Guid.NewGuid();
        (Sale sale, _) = await SeedPendingSaleAsync(context, "CMS030", 1_000_000m, salespersonId: salespersonId);
        CommissionPolicy policy = CommissionPolicy.CreateDefault(DealerId);
        policy.SetDefaultRate(4m);
        policy.SetSalespersonRate(salespersonId, 7m);
        context.CommissionPolicies.Add(policy);
        await context.SaveChangesAsync();

        await NewHandler(context).Handle(
            new SaleCompletedDomainEvent(sale.Id, sale.CarId, sale.ClientId, sale.FinalPrice, PaymentMethod.Cash), CancellationToken.None);

        SaleCommission commission = await context.SaleCommissions.SingleAsync(c => c.SaleId == sale.Id);
        commission.Rate.Should().Be(7m);
        commission.Amount.Amount.Should().Be(70_000m);
    }

    [Fact]
    public async Task Base_ExcludesTradeInValue()
    {
        using var context = CreateContext();
        (Sale sale, _) = await SeedPendingSaleAsync(context, "CMS040", 1_000_000m, tradeInValue: 300_000m);
        CommissionPolicy policy = CommissionPolicy.CreateDefault(DealerId);
        policy.SetDefaultRate(10m);
        context.CommissionPolicies.Add(policy);
        await context.SaveChangesAsync();

        await NewHandler(context).Handle(
            new SaleCompletedDomainEvent(sale.Id, sale.CarId, sale.ClientId, sale.FinalPrice, PaymentMethod.Cash), CancellationToken.None);

        SaleCommission commission = await context.SaleCommissions.SingleAsync(c => c.SaleId == sale.Id);
        commission.Base.Amount.Should().Be(700_000m);
        commission.Amount.Amount.Should().Be(70_000m);
    }

    [Fact]
    public async Task CalculatingTwice_DoesNotCreateASecondRowOrChangeTheFirst()
    {
        using var context = CreateContext();
        Guid salespersonId = Guid.NewGuid();
        (Sale sale, _) = await SeedPendingSaleAsync(context, "CMS050", 1_000_000m, salespersonId: salespersonId);
        CommissionPolicy policy = CommissionPolicy.CreateDefault(DealerId);
        policy.SetDefaultRate(5m);
        context.CommissionPolicies.Add(policy);
        await context.SaveChangesAsync();

        var domainEvent = new SaleCompletedDomainEvent(sale.Id, sale.CarId, sale.ClientId, sale.FinalPrice, PaymentMethod.Cash);
        await NewHandler(context).Handle(domainEvent, CancellationToken.None);
        await NewHandler(context).Handle(domainEvent, CancellationToken.None);

        List<SaleCommission> commissions = await context.SaleCommissions.Where(c => c.SaleId == sale.Id).ToListAsync();
        commissions.Should().HaveCount(1);
    }

    // Security review (sfc B4.1): domain events travel through the outbox (ProcessOutboxMessagesJob), so
    // SaleCompletedDomainEvent can be processed after the sale was already voided — a sale completed and
    // immediately corrected is not reordered or delayed just for this handler. The commission must not come
    // out Pending (payable) for a sale that is, by the time this runs, actually Voided.
    [Fact]
    public async Task WhenTheSaleWasAlreadyVoidedByTheTimeThisRuns_TheCommissionIsCreatedAlreadyVoided()
    {
        using var context = CreateContext();
        (Sale sale, _) = await SeedPendingSaleAsync(context, "CMS060", 1_000_000m);
        sale.Complete();
        sale.Void("Cliente desistió", Guid.NewGuid(), DateTime.UtcNow);
        await context.SaveChangesAsync();

        await NewHandler(context).Handle(
            new SaleCompletedDomainEvent(sale.Id, sale.CarId, sale.ClientId, sale.FinalPrice, PaymentMethod.Cash), CancellationToken.None);

        SaleCommission commission = await context.SaleCommissions.SingleAsync(c => c.SaleId == sale.Id);
        commission.Status.Should().Be(SaleCommissionStatus.Voided);
    }
}
