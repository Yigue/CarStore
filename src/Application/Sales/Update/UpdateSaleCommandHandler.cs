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

        // Update sale properties
        sale.FinalPrice = command.FinalPrice;
        sale.PaymentMethod = command.PaymentMethod;
        sale.Status = command.Status;
        sale.ContractNumber = command.ContractNumber;
        sale.Comments = command.Comments;

        await context.SaveChangesAsync(cancellationToken);

        return sale.Id;
    }
}
