using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Sales.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.GetSalesFunnel;

/// <summary>
/// sfc B6.1 / RN-042: the full funnel Lead → Quote → Deposit → Sale for one period. Each stage counts what was created
/// (or, for sales, completed) inside the range.
/// </summary>
public sealed record GetSalesFunnelQuery(DateTime From, DateTime To) : IQuery<SalesFunnelResponse>, IReportRangeQuery;

public sealed record SalesFunnelResponse(int Leads, int Quotes, int Deposits, int Sales);

public sealed class GetSalesFunnelQueryValidator : ReportRangeValidator<GetSalesFunnelQuery>;

internal sealed class GetSalesFunnelQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetSalesFunnelQuery, SalesFunnelResponse>
{
    public async Task<Result<SalesFunnelResponse>> Handle(GetSalesFunnelQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<SalesFunnelResponse>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        (DateTime from, DateTime to) = (query.From, query.To);

        int leads = await context.Leads.CountAsync(
            l => l.DealerId == dealerId && l.CreatedAt >= from && l.CreatedAt <= to, cancellationToken);
        int quotes = await context.Quotes.CountAsync(
            q => q.DealerId == dealerId && q.CreatedAt >= from && q.CreatedAt <= to, cancellationToken);
        int deposits = await context.Deposits.CountAsync(
            d => d.DealerId == dealerId && d.CreatedAtUtc >= from && d.CreatedAtUtc <= to, cancellationToken);
        int sales = await context.Sales.CountAsync(
            s => s.DealerId == dealerId && s.Status == SaleStatus.Completed && s.SaleDate >= from && s.SaleDate <= to, cancellationToken);

        return Result.Success(new SalesFunnelResponse(leads, quotes, deposits, sales));
    }
}
