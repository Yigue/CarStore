using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Sales;
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

        // Update sale properties using domain method
        if (command.Status == SaleStatus.Pending)
        {
            sale.Update(
                command.FinalPrice,
                command.PaymentMethod,
                command.ContractNumber,
                command.Comments);
        }
        else if (command.Status == SaleStatus.Completed && sale.Status == SaleStatus.Pending)
        {
            sale.Update(
                command.FinalPrice,
                command.PaymentMethod,
                command.ContractNumber,
                command.Comments);
            sale.Complete();
        }
        else if (command.Status == SaleStatus.Cancelled && sale.Status == SaleStatus.Pending)
        {
            sale.Cancel("Cancelled via update");
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }
}
