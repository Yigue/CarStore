using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Reports.GetPurchasesBySupplier;

/// <summary>
/// sfc B7.4 / RN-039: purchases (PurchaseCost) per supplier for vehicles that entered stock in the period. One row per
/// supplier and currency: amounts in different currencies are never added together.
/// </summary>
public sealed record GetPurchasesBySupplierQuery(DateTime From, DateTime To)
    : IQuery<IReadOnlyList<SupplierPurchasesResponse>>, IReportRangeQuery;

public sealed record SupplierPurchasesResponse(Guid SupplierId, string SupplierName, string Currency, int Vehicles, decimal Total);

public sealed class GetPurchasesBySupplierQueryValidator : ReportRangeValidator<GetPurchasesBySupplierQuery>;

internal sealed class GetPurchasesBySupplierQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetPurchasesBySupplierQuery, IReadOnlyList<SupplierPurchasesResponse>>
{
    public async Task<Result<IReadOnlyList<SupplierPurchasesResponse>>> Handle(GetPurchasesBySupplierQuery query, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return Result.Failure<IReadOnlyList<SupplierPurchasesResponse>>(guard.Error);

        Guid dealerId = tenantService.DealerId;
        var purchases = await context.Cars.AsNoTracking()
            .Where(c => c.DealerId == dealerId && c.SupplierId != null && c.PurchaseCost != null
                        && c.CreatedAt >= query.From && c.CreatedAt <= query.To)
            .Select(c => new { SupplierId = c.SupplierId!.Value, c.PurchaseCost!.Amount, c.PurchaseCost.Currency })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, string> names = await context.Suppliers.AsNoTracking()
            .Where(s => s.DealerId == dealerId)
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        IReadOnlyList<SupplierPurchasesResponse> rows = purchases
            .GroupBy(p => new { p.SupplierId, p.Currency })
            .Select(g => new SupplierPurchasesResponse(
                g.Key.SupplierId, names.GetValueOrDefault(g.Key.SupplierId, string.Empty), g.Key.Currency, g.Count(), g.Sum(p => p.Amount)))
            .OrderByDescending(r => r.Total)
            .ToList();
        return Result.Success(rows);
    }
}
