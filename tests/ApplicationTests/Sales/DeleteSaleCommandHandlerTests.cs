using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Sales.Delete;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Sales;

public class DeleteSaleCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Sale NewPendingSale(Guid? carId = null) =>
        new Sale(
            DealerId,
            carId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            10000m,
            PaymentMethod.Cash,
            "CN-1",
            "initial",
            DateTime.UtcNow);

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_SaleDoesNotExist()
    {
        using var context = CreateContext();
        var handler = new DeleteSaleCommandHandler(context, new FakeDateTimeProvider());
        var missingId = Guid.NewGuid();

        var result = await handler.Handle(new DeleteSaleCommand(missingId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.NotFound(missingId));
    }

    [Fact]
    public async Task Handle_Should_DeleteSale_When_Pending()
    {
        using var context = CreateContext();
        var sale = NewPendingSale();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new DeleteSaleCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(new DeleteSaleCommand(sale.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Sales.Should().NotContain(s => s.Id == sale.Id);
    }

    [Fact]
    public async Task Handle_Should_ReleaseReservedCar_When_DeletingPendingSale()
    {
        // Bug: CreateSaleCommandHandler reserves the car for a Pending sale, but
        // deleting the sale did a raw Remove() with no domain event — unlike
        // Cancel(), which raises SaleCancelledDomainEvent to release the car. The
        // car was left stuck as Reservado with no sale left referencing it.
        using var context = CreateContext();
        var marca = new Marca("Toyota");
        var modelo = new Modelo("Corolla", marca.Id);
        var car = new Car(DealerId, marca, modelo, Color.Red, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Reservado, 4, 5, 1600, 1000, 2020, "AB123CD", "desc", 10000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);

        var sale = NewPendingSale(car.Id);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new DeleteSaleCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(new DeleteSaleCommand(sale.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        car.ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }

    [Fact]
    public async Task Handle_Should_DeleteSale_When_Cancelled()
    {
        using var context = CreateContext();
        var sale = NewPendingSale();
        sale.Cancel("customer changed their mind");
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new DeleteSaleCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(new DeleteSaleCommand(sale.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.Sales.Should().NotContain(s => s.Id == sale.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnConflictFailure_And_NotDelete_When_SaleIsCompleted()
    {
        // Bug 2: completed sales have a FinancialTransaction with DeleteBehavior.Restrict.
        // A hard delete would throw a DbUpdateException (FK violation) — the handler must
        // reject it up front with a domain failure instead.
        using var context = CreateContext();
        var sale = NewPendingSale();
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new DeleteSaleCommandHandler(context, new FakeDateTimeProvider());

        var result = await handler.Handle(new DeleteSaleCommand(sale.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.CannotDeleteCompleted(sale.Id));
        context.Sales.Should().Contain(s => s.Id == sale.Id);
    }
}
