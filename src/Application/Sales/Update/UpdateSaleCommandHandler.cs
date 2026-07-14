using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Sales;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Update;

internal sealed class UpdateSaleCommandHandler(
    IApplicationDbContext context)
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
                break;

            case SaleStatus.Cancelled:
                sale.Cancel("Cancelled via update");
                break;

            default:
                return Result.Failure<Guid>(SalesErrors.CannotEditNonPending(command.Id));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }
}
