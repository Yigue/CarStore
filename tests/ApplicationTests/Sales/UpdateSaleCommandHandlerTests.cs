using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Sales.Update;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Financial.Attributes;
using Domain.Sales;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Sales;

public class UpdateSaleCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Sale NewPendingSale() =>
        new Sale(
            DealerId,
            Guid.NewGuid(),
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
        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        var missingId = Guid.NewGuid();
        var command = new UpdateSaleCommand(missingId, 5000m, PaymentMethod.Cash, SaleStatus.Pending, "CN-2", "x");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.NotFound(missingId));
    }

    [Fact]
    public async Task Handle_Should_UpdateSale_When_Pending()
    {
        using var context = CreateContext();
        var sale = NewPendingSale();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        var command = new UpdateSaleCommand(sale.Id, 12345m, PaymentMethod.BankTransfer, SaleStatus.Pending, "CN-UPDATED", "updated");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Sales.FirstAsync(s => s.Id == sale.Id);
        updated.FinalPrice.Amount.Should().Be(12345m);
        updated.ContractNumber.Should().Be("CN-UPDATED");
        updated.Comments.Should().Be("updated");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflictFailure_When_SaleIsNotPending()
    {
        // A completed sale must NOT throw (was causing a 500) — it must return a Result failure.
        using var context = CreateContext();
        var sale = NewPendingSale();
        sale.Complete();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        var command = new UpdateSaleCommand(sale.Id, 999m, PaymentMethod.Cash, SaleStatus.Pending, "CN-3", "y");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.CannotEditNonPending(sale.Id));
    }

    [Fact]
    public async Task Handle_Should_KeepExistingContractNumberAndComments_When_NotProvided()
    {
        // Bug 3: ContractNumber/Comments are optional on update. A null value must NOT
        // overwrite the sale's existing value with an empty string.
        using var context = CreateContext();
        var sale = NewPendingSale();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        var command = new UpdateSaleCommand(sale.Id, 12345m, PaymentMethod.BankTransfer, SaleStatus.Pending);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Sales.FirstAsync(s => s.Id == sale.Id);
        updated.FinalPrice.Amount.Should().Be(12345m);
        updated.ContractNumber.Should().Be("CN-1");
        updated.Comments.Should().Be("initial");
    }

    [Fact]
    public async Task Handle_Should_AssignSalesperson_When_Provided()
    {
        using var context = CreateContext();
        var sale = NewPendingSale();
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var salespersonId = Guid.NewGuid();
        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        var command = new UpdateSaleCommand(sale.Id, 12345m, PaymentMethod.BankTransfer, SaleStatus.Pending, SalespersonId: salespersonId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Sales.FirstAsync(s => s.Id == sale.Id);
        updated.SalespersonId.Should().Be(salespersonId);
    }

    [Fact]
    public async Task Handle_Should_KeepExistingSalesperson_When_NotProvided()
    {
        using var context = CreateContext();
        var sale = NewPendingSale();
        var originalSalespersonId = Guid.NewGuid();
        sale.AssignSalesperson(originalSalespersonId);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        var command = new UpdateSaleCommand(sale.Id, 12345m, PaymentMethod.BankTransfer, SaleStatus.Pending);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.Sales.FirstAsync(s => s.Id == sale.Id);
        updated.SalespersonId.Should().Be(originalSalespersonId);
    }

    // ── CAT-01: the unit's service status moves with the sale, in the same transaction ───────

    private static Car SeedCar(TestApplicationDbContext context, string patente, StatusServiceCar status)
    {
        var marca = new Marca($"Marca{patente}");
        var modelo = new Modelo($"Modelo{patente}", marca.Id);
        var car = new Car(
            DealerId, marca, modelo, Color.Black, TypeCar.Sedan, StatusCar.New, status,
            4, 5, 2000, 0, 2024, patente, "sale sync test car", 15000m, DateTime.UtcNow);
        context.Marca.Add(marca);
        context.Modelo.Add(modelo);
        context.Cars.Add(car);
        return car;
    }

    private static Sale NewPendingSaleFor(Guid carId) =>
        new Sale(DealerId, carId, Guid.NewGuid(), 10000m, PaymentMethod.Cash, "CN-1", "sync", DateTime.UtcNow);

    [Fact]
    public async Task Handle_Should_MarkTheCarSold_When_TheSaleIsCompleted()
    {
        using var context = CreateContext();
        Car car = SeedCar(context, "UPD001", StatusServiceCar.Reservado);
        var sale = NewPendingSaleFor(car.Id);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        var result = await handler.Handle(
            new UpdateSaleCommand(sale.Id, 10000m, PaymentMethod.Cash, SaleStatus.Completed, "CN-1", "done"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await context.Cars.AsNoTracking().FirstAsync(c => c.Id == car.Id);
        persisted.ServiceCar.Should().Be(
            StatusServiceCar.Vendido,
            "the catalogue reads this column — waiting for the outbox keeps a sold unit on sale");
    }

    [Fact]
    public async Task Handle_Should_ReleaseTheCar_When_TheSaleIsCancelled()
    {
        using var context = CreateContext();
        Car car = SeedCar(context, "UPD002", StatusServiceCar.Reservado);
        var sale = NewPendingSaleFor(car.Id);
        context.Sales.Add(sale);
        await context.SaveChangesAsync();

        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        await handler.Handle(
            new UpdateSaleCommand(sale.Id, 10000m, PaymentMethod.Cash, SaleStatus.Cancelled, "CN-1", "no deal"),
            CancellationToken.None);

        var persisted = await context.Cars.AsNoTracking().FirstAsync(c => c.Id == car.Id);
        persisted.ServiceCar.Should().Be(StatusServiceCar.Disponible);
    }

    [Fact]
    public async Task Handle_Should_NotReleaseTheCar_When_AnotherSaleStillHoldsIt()
    {
        using var context = CreateContext();
        Car car = SeedCar(context, "UPD003", StatusServiceCar.Reservado);
        var cancelled = NewPendingSaleFor(car.Id);
        var surviving = NewPendingSaleFor(car.Id);
        context.Sales.AddRange(cancelled, surviving);
        await context.SaveChangesAsync();

        var handler = new UpdateSaleCommandHandler(context, new FakeDateTimeProvider());
        await handler.Handle(
            new UpdateSaleCommand(cancelled.Id, 10000m, PaymentMethod.Cash, SaleStatus.Cancelled, "CN-1", "no deal"),
            CancellationToken.None);

        var persisted = await context.Cars.AsNoTracking().FirstAsync(c => c.Id == car.Id);
        persisted.ServiceCar.Should().Be(
            StatusServiceCar.Reservado,
            "cancelling one draft must not hand back a unit another live sale is holding");
    }
}
