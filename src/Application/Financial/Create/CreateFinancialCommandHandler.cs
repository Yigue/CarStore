using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial;
using SharedKernel;

namespace Application.Financial.Create;

internal sealed class CreateFinancialCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<CreateFinancialCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateFinancialCommand command, CancellationToken cancellationToken)
    {
        var financial = new FinancialTransaction(
            command.Type,
            command.Amount,
            command.Description,
            command.PaymentMethod,
            command.Category,
            command.Car,
            command.Client,
            command.Sale);

        context.Transactions.Add(financial);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(financial.Id);
    }
}
