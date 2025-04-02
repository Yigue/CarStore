using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.Update;

internal sealed class UpdateFinancialCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<UpdateFinancialCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateFinancialCommand command, CancellationToken cancellationToken)
    {
        FinancialTransaction financial = await context.Transactions
            .SingleOrDefaultAsync(f => f.Id == command.Id, cancellationToken);

        if (financial is null)
        {
            return Result.Failure<Guid>(FinancialErrors.NotFound(command.Id));
        }

        financial.Update(
            command.Type,
            command.Amount,
            command.Description,
            command.PaymentMethod,
            command.ReferenceNumber,
            command.TransactionDate,
            command.CategoryId,
            command.CarId,
            command.ClientId,
            command.SaleId
        );

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(financial.Id);
    }
}
