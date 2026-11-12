using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Currency.GetExchangeRates;

/// <summary>sfc B0.2.3: reads the caller's dealer's rates. Pinned to the dealer in the handler as well as by the query filter.</summary>
internal sealed class GetExchangeRatesQueryHandler(IApplicationDbContext context, ICurrentTenantService tenantService)
    : IQueryHandler<GetExchangeRatesQuery, IReadOnlyList<ExchangeRateResponse>>
{
    public async Task<Result<IReadOnlyList<ExchangeRateResponse>>> Handle(
        GetExchangeRatesQuery request, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ExchangeRateResponse>>(guard.Error);
        }

        Guid dealerId = tenantService.DealerId;
        IReadOnlyList<ExchangeRateResponse> rates = await context.DealerExchangeRates
            .AsNoTracking()
            .Where(r => r.DealerId == dealerId)
            .OrderByDescending(r => r.EffectiveAtUtc)
            .Select(r => new ExchangeRateResponse(r.Id, r.BaseCurrency, r.QuoteCurrency, r.Rate, r.EffectiveAtUtc, r.CreatedBy))
            .ToListAsync(cancellationToken);

        return Result.Success(rates);
    }
}
