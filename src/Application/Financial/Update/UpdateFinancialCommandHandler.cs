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

        financial.Type = command.Type;
        financial.Amount = command.Amount;
        financial.Description = command.Description;
        financial.PaymentMethod = command.PaymentMethod;
        financial.ReferenceNumber = command.ReferenceNumber;
        financial.TransactionDate = command.TransactionDate;
        financial.CategoryId = command.Category.Id;    
        financial.CarId = command.Car.Id;
        financial.ClientId = command.Client.Id;
        financial.SaleId = command.Sale.Id;
    

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(financial.Id);
    }
}
