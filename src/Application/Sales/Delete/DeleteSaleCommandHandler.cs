using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Sales;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Sales.Delete;

internal sealed class DeleteSaleCommandHandler(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
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

        // Completed sales have a linked FinancialTransaction with DeleteBehavior.Restrict —
        // a hard delete would throw a DbUpdateException (FK violation) and surface as a 500.
        // Return a domain failure instead.
        if (sale.Status == SaleStatus.Completed)
        {
            return Result.Failure<Guid>(SalesErrors.CannotDeleteCompleted(sale.Id));
        }

        // A Pending sale reserves its car (see CreateSaleCommandHandler). Deleting the
        // sale outright — unlike Cancel(), which raises SaleCancelledDomainEvent to
        // release it — bypasses that release. Mirror it here so the car doesn't stay
        // stuck as Reservado with no sale referencing it.
        Car? car = await context.Cars.FindAsync([sale.CarId], cancellationToken);
        car?.MarkAsAvailable(dateTimeProvider.UtcNow);

        context.Sales.Remove(sale);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(sale.Id);
    }
}