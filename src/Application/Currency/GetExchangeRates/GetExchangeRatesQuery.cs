using Application.Abstractions.Messaging;

namespace Application.Currency.GetExchangeRates;

/// <summary>sfc B0.2.3: the caller's dealer's exchange-rate history, most recent effective rate first.</summary>
public sealed record GetExchangeRatesQuery : IQuery<IReadOnlyList<ExchangeRateResponse>>;

public sealed record ExchangeRateResponse(
    Guid Id,
    string BaseCurrency,
    string QuoteCurrency,
    decimal Rate,
    DateTime EffectiveAtUtc,
    Guid CreatedBy);
