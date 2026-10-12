using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Sales.Events;
using Application.Sales.Void;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Domain.Sales.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Application.UnitTests.Sales;

/// <summary>sfc B1.2 / ADR C3 / RN-006 — voiding a Completed sale and reverting its car.</summary>
public class VoidSaleCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TestApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class ThrowingSaveInterceptor : SaveChangesInterceptor
    {
        public bool Armed { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Armed ? throw new InvalidOperationException("boom") : base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static async Task<(Sale Sale, Car Car)> SeedCompletedSaleAsync(TestApplicationDbContext context, Car? car = null)
    {
        car ??= NewSoldCar();
        context.Cars.Add(car);
        Sale sale = NewSale(car.Id);
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();
        return (sale, car);
    }

    private static VoidSaleCommandHandler CreateHandler(TestApplicationDbContext context, Guid? dealerId = null)
    {
        var tenant = new Mock<ICurrentTenantService>();
        tenant.SetupGet(t => t.HasTenant).Returns(true);
        tenant.SetupGet(t => t.DealerId).Returns(dealerId ?? DealerId);
        var user = new Mock<IUserContext>();
        user.SetupGet(u => u.UserId).Returns(ActorId);
        return new VoidSaleCommandHandler(context, user.Object, tenant.Object, new FakeDateTimeProvider());
    }

    private static Car NewSoldCar() => new(DealerId, new Marca("Toyota"), new Modelo("Corolla", Guid.NewGuid()),
        Color.Red, TypeCar.Sedan, StatusCar.New, StatusServiceCar.Vendido, 4, 5, 1600, 1000, 2020, "AB123CD",
        "desc", 10000m, DateTime.UtcNow);

    private static Sale NewSale(Guid carId) => new(DealerId, carId, Guid.NewGuid(), 10000m,
        PaymentMethod.Cash, "CN-1", "", DateTime.UtcNow);

    [Fact]
    public async Task Handle_Should_VoidCompletedSale_And_TakeActorFromTenantUserContext()
    {
        using var context = CreateContext();
        (Sale sale, Car car) = await SeedCompletedSaleAsync(context);

        var result = await CreateHandler(context).Handle(new VoidSaleCommand(sale.Id, "Cliente desistió"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sale.Status.Should().Be(SaleStatus.Voided);
        car.ServiceCar.Should().Be(StatusServiceCar.Disponible);
        sale.DomainEvents.OfType<SaleVoidedDomainEvent>().Single().ActorId.Should().Be(ActorId);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_ReasonIsBlank()
    {
        using var context = CreateContext();
        Sale sale = NewSale(Guid.NewGuid());
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new VoidSaleCommand(sale.Id, "  "), CancellationToken.None);

        result.Error.Should().Be(SalesErrors.VoidReasonRequired());
        sale.Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_SaleIsNotCompleted()
    {
        using var context = CreateContext();
        Sale sale = NewSale(Guid.NewGuid());
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new VoidSaleCommand(sale.Id, "x"), CancellationToken.None);

        result.Error.Should().Be(SalesErrors.OnlyCompletedCanBeVoided(sale.Id));
        sale.Status.Should().Be(SaleStatus.Pending);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_SaleBelongsToAnotherDealer()
    {
        using var context = CreateContext();
        Sale sale = NewSale(Guid.NewGuid());
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context, Guid.NewGuid())
            .Handle(new VoidSaleCommand(sale.Id, "x"), CancellationToken.None);

        result.Error.Should().Be(SalesErrors.NotFound(sale.Id));
        sale.Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public async Task Handle_Should_RevertCarAndVoidSale_InOneSave()
    {
        var interceptor = new CountingSaveInterceptor();
        using var context = new TestApplicationDbContext(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(interceptor).Options);
        (Sale sale, Car car) = await SeedCompletedSaleAsync(context);
        interceptor.Count = 0;

        var result = await CreateHandler(context).Handle(new VoidSaleCommand(sale.Id, "x"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        interceptor.Count.Should().Be(1);
        sale.Status.Should().Be(SaleStatus.Voided);
        car.ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }

    private sealed class CountingSaveInterceptor : SaveChangesInterceptor
    {
        public int Count { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task Handle_Should_ChangeNeitherSaleNorCar_When_SaveFails()
    {
        var interceptor = new ThrowingSaveInterceptor();
        string dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(dbName).AddInterceptors(interceptor).Options;
        Guid saleId, carId;
        using (var seed = new TestApplicationDbContext(options))
        {
            (Sale s, Car c) = await SeedCompletedSaleAsync(seed);
            saleId = s.Id;
            carId = c.Id;
        }

        using (var context = new TestApplicationDbContext(options))
        {
            interceptor.Armed = true;
            Func<Task> act = () => CreateHandler(context).Handle(new VoidSaleCommand(saleId, "x"), CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        interceptor.Armed = false;
        using var verify = new TestApplicationDbContext(options);
        (await verify.Sales.SingleAsync(s => s.Id == saleId)).Status.Should().Be(SaleStatus.Completed);
        (await verify.Cars.SingleAsync(c => c.Id == carId)).ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }

    [Fact]
    public async Task Handle_Should_Fail_AndKeepSaleCompleted_When_CarIsMissing()
    {
        using var context = CreateContext();
        Sale sale = NewSale(Guid.NewGuid());
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new VoidSaleCommand(sale.Id, "x"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.NotFound(sale.CarId));
        context.ChangeTracker.Clear();
        (await context.Sales.SingleAsync(s => s.Id == sale.Id)).Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public async Task Handle_Should_Fail_AndKeepSaleCompleted_When_CarIsNotSold()
    {
        using var context = CreateContext();
        Car car = NewSoldCar();
        car.MarkAsAvailable(DateTime.UtcNow);
        (Sale sale, _) = await SeedCompletedSaleAsync(context, car);

        var result = await CreateHandler(context).Handle(new VoidSaleCommand(sale.Id, "x"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CarErrors.NotSold(car.Id));
        context.ChangeTracker.Clear();
        (await context.Sales.SingleAsync(s => s.Id == sale.Id)).Status.Should().Be(SaleStatus.Completed);
    }

    [Fact]
    public async Task SaleVoidedEvent_Should_NotTouchTheCar_BecauseTheHandlerAlreadyRevertedIt()
    {
        using var context = CreateContext();
        Car car = NewSoldCar();
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        var handler = new SaleVoidedDomainEventHandler(NullLogger<SaleVoidedDomainEventHandler>.Instance);

        await handler.Handle(new SaleVoidedDomainEvent(Guid.NewGuid(), car.Id, "motivo", ActorId, DateTime.UtcNow),
            CancellationToken.None);

        car.ServiceCar.Should().Be(StatusServiceCar.Vendido);
    }
}
