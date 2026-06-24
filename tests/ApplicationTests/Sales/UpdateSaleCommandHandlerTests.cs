using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Sales.Update;
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
        var handler = new UpdateSaleCommandHandler(context);
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

        var handler = new UpdateSaleCommandHandler(context);
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

        var handler = new UpdateSaleCommandHandler(context);
        var command = new UpdateSaleCommand(sale.Id, 999m, PaymentMethod.Cash, SaleStatus.Pending, "CN-3", "y");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SalesErrors.CannotEditNonPending(sale.Id));
    }
}
