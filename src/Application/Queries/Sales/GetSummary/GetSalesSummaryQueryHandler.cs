using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Sales.GetSummary;

internal sealed class GetSalesSummaryQueryHandler
    : IQueryHandler<GetSalesSummaryQuery, SalesSummaryResponse>
{
    private readonly IApplicationDbContext _context;

    public GetSalesSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SalesSummaryResponse>> Handle(
        GetSalesSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var sales = await _context.Sales
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var totalAmount = sales.Sum(s => s.FinalPrice.Amount);
        var totalCount = sales.Count;
        var averagePrice = totalCount > 0 ? totalAmount / totalCount : 0;

        var pending = sales.Count(s => s.Status == SaleStatus.Pending);
        var completed = sales.Count(s => s.Status == SaleStatus.Completed);
        var cancelled = sales.Count(s => s.Status == SaleStatus.Cancelled);

        var response = new SalesSummaryResponse(
            totalAmount,
            totalCount,
            averagePrice,
            new SalesByStatus(pending, completed, cancelled));

        return Result.Success(response);
    }
}