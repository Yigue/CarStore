using Application.Abstractions.Data;
using Domain.Currency;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.Currency;

/// <summary>
/// sfc B0.2 / ADR C2: the dealer's latest manual rate for a currency pair that is already effective at
/// <paramref name="asOfUtc"/>. The pair matches in either direction; rates loaded for a future date are ignored.
/// </summary>
internal static class ExchangeRateLookup
{
    public static Task<DealerExchangeRate?> FindLatestEffectiveRateAsync(
        this IApplicationDbContext context,
        Guid dealerId,
        CurrencyCode first,
        CurrencyCode second,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        string a = first.Value;
        string b = second.Value;

        return context.DealerExchangeRates
            .Where(r => r.DealerId == dealerId
                        && r.EffectiveAtUtc <= asOfUtc
                        && ((r.BaseCurrency == a && r.QuoteCurrency == b) || (r.BaseCurrency == b && r.QuoteCurrency == a)))
            .OrderByDescending(r => r.EffectiveAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
