using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Sales;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Update;

internal sealed class UpdateSaleCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateSaleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateSaleCommand command, CancellationToken cancellationToken)
    {
        Sale? sale = await context.Sales
            .FirstOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (sale is null)
        {
            return Result.Failure<Guid>(SalesErrors.NotFound(command.Id));
        }

        // Domain rule: only pending sales can be mutated. Guard here and return a
        // conflict result instead of letting the domain methods throw (which surfaced as a 500).
        if (sale.Status != SaleStatus.Pending)
        {
            return Result.Failure<Guid>(SalesErrors.CannotEditNonPending(command.Id));
        }

        // Only overwrite ContractNumber/Comments when the caller actually sent a value —
        // both are optional on update, so a null keeps the sale's current value.
        string contractNumber = command.ContractNumber ?? sale.ContractNumber;
        string comments = command.Comments ?? sale.Comments;

        // Same "null keeps current value" convention as ContractNumber/Comments above —
        // there is currently no way to explicitly clear a previously assigned salesperson.
        sale.AssignSalesperson(command.SalespersonId ?? sale.SalespersonId);

        switch (command.Status)
        {
            case SaleStatus.Pending:
                sale.Update(
                    command.FinalPrice,
                    command.PaymentMethod,
                    contractNumber,
                    comments);
                break;

            case SaleStatus.Completed:
                sale.Update(
                    command.FinalPrice,
                    command.PaymentMethod,
                    contractNumber,
                    comments);
                sale.Complete();

                // CAT-01: same transaction as the completion, for the same reason as
                // CreateSaleCommandHandler — the public catalogue reads Car.ServiceCar, so
                // leaving the sync to the outbox leaves a sold unit on sale for as long as the
                // job takes, or forever if it fails. The outbox handler stays the safety net.
                await SyncCarWithCompletedSaleAsync(sale.CarId, cancellationToken);
                break;

            case SaleStatus.Cancelled:
                sale.Cancel("Cancelled via update");

                // The mirror image: a cancelled sale gives the unit back to the floor, unless
                // some other live sale still holds it.
                await ReleaseCarIfUnheldAsync(sale.CarId, sale.Id, cancellationToken);
                break;

            default:
                return Result.Failure<Guid>(SalesErrors.CannotEditNonPending(command.Id));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }

    private async Task SyncCarWithCompletedSaleAsync(Guid carId, CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);

        car?.MarkAsSold(dateTimeProvider.UtcNow);
    }

    private async Task ReleaseCarIfUnheldAsync(Guid carId, Guid cancelledSaleId, CancellationToken cancellationToken)
    {
        // Never hand back a unit another operation is still holding: a second pending sale, or a
        // completed one. Releasing unconditionally is how a sold car reappears in the catalogue
        // because an unrelated draft next to it was cancelled.
        bool stillHeld = await context.Sales
            .AnyAsync(
                s => s.CarId == carId
                    && s.Id != cancelledSaleId
                    && (s.Status == SaleStatus.Pending || s.Status == SaleStatus.Completed),
                cancellationToken);

        if (stillHeld)
        {
            return;
        }

        Car? car = await context.Cars
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);

        car?.MarkAsAvailable(dateTimeProvider.UtcNow);
    }
}
