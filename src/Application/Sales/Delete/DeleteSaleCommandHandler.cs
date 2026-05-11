using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Sales;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Delete;

internal sealed class DeleteSaleCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteSaleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(DeleteSaleCommand command, CancellationToken cancellationToken)
    {
        Sale? sale = await context.Sales
            .SingleOrDefaultAsync(s => s.Id == command.Id, cancellationToken);

        if (sale is null)
        {
            return Result.Failure<Guid>(SalesErrors.NotFound(command.Id));
        }

        context.Sales.Remove(sale);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }
}