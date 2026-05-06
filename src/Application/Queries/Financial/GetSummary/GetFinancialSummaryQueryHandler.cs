using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Financial.GetSummary;

internal sealed class GetFinancialSummaryQueryHandler
    : IQueryHandler<GetFinancialSummaryQuery, FinancialSummaryResponse>
{
    private readonly IApplicationDbContext _context;

    public GetFinancialSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<FinancialSummaryResponse>> Handle(
        GetFinancialSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var transactions = await _context.Transactions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalIncome = transactions
            .Where(t => t.Type == TransactionType.Income)
            .Sum(t => t.Amount.Amount);

        var totalExpenses = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => t.Amount.Amount);

        var balance = totalIncome - totalExpenses;
        var entryCount = transactions.Count;

        var response = new FinancialSummaryResponse(
            totalIncome,
            totalExpenses,
            balance,
            entryCount);

        return Result.Success(response);
    }
}