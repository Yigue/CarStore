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
        Sale sale = NewSale(Guid.NewGuid());
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context).Handle(new VoidSaleCommand(sale.Id, "Cliente desistió"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sale.Status.Should().Be(SaleStatus.Voided);
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
    public async Task SaleVoidedEvent_Should_RevertCarToDisponible()
    {
        using var context = CreateContext();
        Car car = NewSoldCar();
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        var handler = new SaleVoidedDomainEventHandler(context, new FakeDateTimeProvider(),
            NullLogger<SaleVoidedDomainEventHandler>.Instance);

        await handler.Handle(new SaleVoidedDomainEvent(Guid.NewGuid(), car.Id, "motivo", ActorId, DateTime.UtcNow),
            CancellationToken.None);

        car.ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }

    [Fact]
    public async Task SaleVoidedEvent_Should_NotThrow_When_CarIsNotSold()
    {
        using var context = CreateContext();
        Car car = NewSoldCar();
        car.MarkAsAvailable(DateTime.UtcNow);
        context.Cars.Add(car);
        await context.SaveChangesAsync();
        var handler = new SaleVoidedDomainEventHandler(context, new FakeDateTimeProvider(),
            NullLogger<SaleVoidedDomainEventHandler>.Instance);

        Func<Task> act = () => handler.Handle(
            new SaleVoidedDomainEvent(Guid.NewGuid(), car.Id, "motivo", ActorId, DateTime.UtcNow), CancellationToken.None);

        await act.Should().NotThrowAsync();
        car.ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }
}
