using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Financial.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Financial.GetSummary;

internal sealed class GetFinancialSummaryQueryHandler
    : IQueryHandler<GetFinancialSummaryQuery, FinancialSummaryResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentTenantService _tenantService;

    public GetFinancialSummaryQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    {
        _context = context;
        _tenantService = tenantService;
    }

    public async Task<Result<FinancialSummaryResponse>> Handle(
        GetFinancialSummaryQuery query,
        CancellationToken cancellationToken)
    {
        // REQ-FIN-TENANT-001: Enforce tenant context check at handler layer
        var tenantGuard = TenantGuard.EnsureHasTenant(_tenantService);
        if (tenantGuard.IsFailure)
        {
            return Result.Failure<FinancialSummaryResponse>(tenantGuard.Error);
        }

        var baseQuery = _context.Transactions
            .AsNoTracking()
            .Where(t => t.DealerId == _tenantService.DealerId);

        if (query.From.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.TransactionDate >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.TransactionDate <= query.To.Value);
        }

        // REQ-FIN-SUMMARY-002: Perform single-query aggregation in SQL
        // Use EF.Property<decimal> to refer directly to the database column of the value-converted Amount property,
        // resolving translation limitations of nested group aggregates.
        var summaryData = await baseQuery
            .GroupBy(t => 1)
            .Select(g => new
            {
                TotalIncome = g.Where(t => t.Type == TransactionType.Income).Sum(t => (decimal?)EF.Property<decimal>(t, "Amount")) ?? 0m,
                TotalExpenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => (decimal?)EF.Property<decimal>(t, "Amount")) ?? 0m,
                EntryCount = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalIncome = summaryData?.TotalIncome ?? 0m;
        var totalExpenses = summaryData?.TotalExpenses ?? 0m;
        var balance = totalIncome - totalExpenses;
        var entryCount = summaryData?.EntryCount ?? 0;

        var response = new FinancialSummaryResponse(
            totalIncome,
            totalExpenses,
            balance,
            entryCount);

        return Result.Success(response);
    }
}