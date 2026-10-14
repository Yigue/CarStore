using Application.Abstractions.Tenancy;
using Application.Deposits.Refund;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Deposits;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Deposits;

/// <summary>
/// sfc B1.3.5 / RN-003: the dealer records a manual refund (no money moves through the system). Refunding an Active
/// deposit stops it from holding the car, which goes back to Disponible unless something else still holds it.
/// </summary>
public class RefundDepositCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static RefundDepositCommandHandler Handler(TestApplicationDbContext context, Guid? dealerId = null)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(dealerId ?? DealerId);
        return new RefundDepositCommandHandler(context, new FakeDateTimeProvider { UtcNow = Now }, tenant.Object);
    }

    private static async Task<(Car Car, Deposit Deposit)> SeedAsync(TestApplicationDbContext context)
    {
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Disponible,
            4, 5, 1600, 1000, 2021, "RFD001", "desc", 10000m, Now.AddDays(-30));
        car.Reserve(Now.AddDays(-2));
        var deposit = Deposit.Create(DealerId, car.Id, Guid.NewGuid(), null, 1000m, "ARS", null, Now.AddDays(-2),
            new DepositCarContext(StatusServiceCar.Disponible, null, false, "ARS")).Value;
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        context.Deposits.Add(deposit);
        await context.SaveChangesAsync();
        return (car, deposit);
    }

    [Fact]
    public async Task Handle_Should_RecordTheRefund_AndReleaseTheCar()
    {
        using var context = CreateContext();
        var (_, deposit) = await SeedAsync(context);

        var result = await Handler(context).Handle(new RefundDepositCommand(deposit.Id, 1000m, "Cliente desistió"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var saved = await context.Deposits.SingleAsync();
        saved.Status.Should().Be(DepositStatus.Refunded);
        saved.RefundReason.Should().Be("Cliente desistió");
        (await context.Cars.SingleAsync()).ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }

    [Fact]
    public async Task Handle_Should_RejectARefundAboveTheDepositAmount()
    {
        using var context = CreateContext();
        var (_, deposit) = await SeedAsync(context);

        var result = await Handler(context).Handle(new RefundDepositCommand(deposit.Id, 5000m, "x"), CancellationToken.None);

        result.Error.Code.Should().Be("Deposits.RefundExceedsAmount");
        (await context.Cars.SingleAsync()).ServiceCar.Should().Be(StatusServiceCar.Reservado);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_ForAnotherDealersDeposit()
    {
        using var context = CreateContext();
        var (_, deposit) = await SeedAsync(context);

        var result = await Handler(context, Guid.NewGuid()).Handle(new RefundDepositCommand(deposit.Id, 100m, "x"), CancellationToken.None);

        result.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
    }
}
