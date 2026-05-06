using SharedKernel;

namespace Application.Queries.Financial.GetSummary;

public sealed record FinancialSummaryResponse(
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal Balance,
    int EntryCount);