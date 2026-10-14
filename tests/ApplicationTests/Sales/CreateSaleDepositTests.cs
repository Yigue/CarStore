using Application.Abstractions.Tenancy;
using Application.Deposits.Apply;
using Application.Sales.Create;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Clients;
using Domain.Currency;
using Domain.Deposits;
using Domain.Financial.Attributes;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using Domain.Shared.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Sales;

/// <summary>
/// sfc B1.3 / RN-001 / RN-004 / design B1 "Aplicar": an Active deposit reserves the car for its buyer. A sale to someone
/// else is refused; a sale to the same buyer takes the deposit as down payment by default (converted with the deposit's
/// snapshot when currencies differ), and completing the sale marks the deposit Applied.
/// </summary>
public class CreateSaleDepositTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CreateSaleCommandHandler Handler(TestApplicationDbContext context)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(DealerId);
        return new CreateSaleCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now }, tenant.Object);
    }

    private static Client NewClient(string dni) =>
        new(DealerId, "Ana", "Perez", dni, $"{dni}@buyer.com", "111", "Addr", Now.AddDays(-30));

    private static async Task<(Car Car, Client Buyer, Client Other)> SeedReservedByDepositAsync(
        TestApplicationDbContext context, string depositCurrency = "ARS", ExchangeRateSnapshot? snapshot = null)
    {
        var marca = new Marca("Ford");
        var modelo = new Modelo("Focus", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2020, "DPS001", "desc", 10000m, Now.AddDays(-30));
        var buyer = NewClient("30111222");
        var other = NewClient("30999888");
        // Client ids are assigned by EF on Add: save the buyers before the deposit references one.
        context.Clients.AddRange(buyer, other);
        await context.SaveChangesAsync();
        var deposit = Deposit.Create(DealerId, car.Id, buyer.Id, null, 1000m, depositCurrency, snapshot, Now.AddDays(-1),
            new DepositCarContext(StatusServiceCar.Disponible, null, false, "ARS")).Value;
        car.Reserve(Now.AddDays(-1));
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Deposits.Add(deposit);
        await context.SaveChangesAsync();
        return (car, buyer, other);
    }

    private static CreateSaleCommand Sale(Guid carId, Guid clientId) =>
        new(carId, clientId, 10000m, PaymentMethod.Cash, "CN-DEP", "c");

    [Fact]
    public async Task Handle_Should_RefuseASale_ToAnotherBuyer_WhileADepositIsActive()
    {
        using var context = CreateContext();
        var (car, _, other) = await SeedReservedByDepositAsync(context);

        var result = await Handler(context).Handle(Sale(car.Id, other.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("DEPOSIT_BLOCKS_OTHER_BUYER");
        (await context.Sales.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_TakeTheDepositAsDownPayment_ForItsBuyer()
    {
        using var context = CreateContext();
        var (car, buyer, _) = await SeedReservedByDepositAsync(context);

        var result = await Handler(context).Handle(Sale(car.Id, buyer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code + ": " + result.Error.Description : string.Empty);
        (await context.Sales.SingleAsync()).DownPayment.Should().Be(new Money(1000m, "ARS"));
    }

    [Fact]
    public async Task Handle_Should_ConvertTheDeposit_WithItsSnapshot_WhenCurrenciesDiffer()
    {
        using var context = CreateContext();
        // USD deposit on an ARS car: 1 USD = 1100 ARS, frozen when the deposit was taken.
        var snapshot = new ExchangeRateSnapshot("USD", "ARS", 1100m, Now.AddDays(-1), Guid.NewGuid());
        var (car, buyer, _) = await SeedReservedByDepositAsync(context, "USD", snapshot);

        var result = await Handler(context).Handle(Sale(car.Id, buyer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code + ": " + result.Error.Description : string.Empty);
        (await context.Sales.SingleAsync()).DownPayment.Should().Be(new Money(1_100_000m, "ARS"));
    }

    [Fact]
    public async Task SaleCompleted_Should_ApplyTheActiveDeposit()
    {
        using var context = CreateContext();
        var (car, buyer, _) = await SeedReservedByDepositAsync(context);
        var saleId = Guid.NewGuid();

        await new ApplyDepositOnSaleCompletedHandler(context).Handle(
            new SaleCompletedDomainEvent(saleId, car.Id, buyer.Id, new Money(10000m, "ARS"), PaymentMethod.Cash),
            CancellationToken.None);

        var deposit = await context.Deposits.SingleAsync();
        deposit.Status.Should().Be(DepositStatus.Applied);
        deposit.AppliedSaleId.Should().Be(saleId);
    }
}
