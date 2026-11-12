using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.CurrencyReview;

/// <summary>
/// sfc B0.1.5: lists the caller's cars whose <c>PurchaseCost</c> currency differs from the price currency. Pinned to the
/// caller's dealer in the handler as well as by the global query filter.
/// </summary>
internal sealed class GetCurrencyReviewQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetCurrencyReviewQuery, IReadOnlyList<CurrencyReviewItemResponse>>
{
    public async Task<Result<IReadOnlyList<CurrencyReviewItemResponse>>> Handle(
        GetCurrencyReviewQuery request, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<CurrencyReviewItemResponse>>(guard.Error);
        }

        Guid dealerId = tenantService.DealerId;
        IReadOnlyList<CurrencyReviewItemResponse> rows = await context.Cars
            .AsNoTracking()
            .Where(c => c.DealerId == dealerId
                        && c.PurchaseCost != null
                        && c.PurchaseCost.Currency != c.Price.Currency)
            .Select(c => new CurrencyReviewItemResponse(
                c.Id,
                c.Price.Currency,
                c.PurchaseCost!.Currency,
                c.Price.Amount,
                c.PurchaseCost!.Amount))
            .ToListAsync(cancellationToken);

        return Result.Success(rows);
    }
}
