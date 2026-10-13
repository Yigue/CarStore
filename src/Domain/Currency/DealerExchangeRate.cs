using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Currency;

/// <summary>
/// sfc B0.2.2 / ADR C2: a manual exchange rate loaded by the dealer. Append-only: a rate is never edited or deleted,
/// a new one is loaded instead, so every operation can keep pointing at the rate it was captured from.
/// <c>Rate</c> means: 1 <c>BaseCurrency</c> = <c>Rate</c> <c>QuoteCurrency</c>.
/// </summary>
public sealed class DealerExchangeRate : Entity
{
    public string BaseCurrency { get; private set; } = string.Empty;
    public string QuoteCurrency { get; private set; } = string.Empty;
    public decimal Rate { get; private set; }
    public DateTime EffectiveAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }

    private const decimal MaxRate = 999_999_999_999.999999m;

    // EF Core
    private DealerExchangeRate() { }

    private DealerExchangeRate(Guid dealerId, CurrencyCode baseCurrency, CurrencyCode quoteCurrency, decimal rate, DateTime effectiveAtUtc, Guid createdBy)
    {
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        BaseCurrency = baseCurrency.Value;
        QuoteCurrency = quoteCurrency.Value;
        Rate = rate;
        EffectiveAtUtc = effectiveAtUtc;
        CreatedBy = createdBy;
    }

    public static Result<DealerExchangeRate> Record(
        Guid dealerId, CurrencyCode baseCurrency, CurrencyCode quoteCurrency, decimal rate, DateTime effectiveAtUtc, Guid createdBy)
    {
        if (baseCurrency == quoteCurrency)
            return Result.Failure<DealerExchangeRate>(Error.Validation(
                "Currency.SamePair", "Exchange rate needs two different currencies."));

        if (rate <= 0)
            return Result.Failure<DealerExchangeRate>(Error.Validation(
                "Currency.RateNotPositive", "Exchange rate must be greater than zero."));

        // Persisted as decimal(18,6): at most 12 integer digits and 6 decimals. Anything wider would fail at insert time.
        if (rate > MaxRate || decimal.Round(rate, 6) != rate)
            return Result.Failure<DealerExchangeRate>(Error.Validation(
                "Currency.RateOutOfRange", "Exchange rate allows at most 12 integer digits and 6 decimals."));

        return Result.Success(new DealerExchangeRate(dealerId, baseCurrency, quoteCurrency, rate, effectiveAtUtc, createdBy));
    }
}
