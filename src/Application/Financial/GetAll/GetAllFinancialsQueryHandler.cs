using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.GetAll;

internal sealed class GetAllFinancialsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllFinancialsQuery, IReadOnlyList<FinancialResponses>>
{
    public async Task<Result<IReadOnlyList<FinancialResponses>>> Handle(GetAllFinancialsQuery query, CancellationToken cancellationToken)
    {
        List<FinancialResponses> transactions = await context.Transactions
            .Select(transaction => new FinancialResponses(
                transaction.Id,
                transaction.Type,
                transaction.Amount.Amount,
                transaction.Description,
                transaction.PaymentMethod,
                transaction.ReferenceNumber,
                transaction.TransactionDate,
                transaction.Category,
                transaction.Car,
                transaction.Client,
                transaction.Sale
            ))
            .ToListAsync(cancellationToken);

        return Result.Success((IReadOnlyList<FinancialResponses>)transactions);
    }
}
