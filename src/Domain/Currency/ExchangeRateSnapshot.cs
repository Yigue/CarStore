using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Currency;

/// <summary>
/// sfc B0.2.2 / ADR C2 / D-CURRENCY: the exchange rate frozen into an operation (quote, deposit or sale) when it was
/// registered. Immutable and never recalculated: later rate loads do not touch it.
/// </summary>
public sealed record ExchangeRateSnapshot(
    string Base,
    string Quote,
    decimal Rate,
    DateTime CapturedAtUtc,
    Guid SourceRateId)
{
    /// <summary>
    /// Returns <c>null</c> when the operation is in the vehicle's currency (no conversion, no rate needed). Otherwise a
    /// rate for exactly that pair is required and is copied into the snapshot.
    /// </summary>
    public static Result<ExchangeRateSnapshot?> Capture(
        CurrencyCode vehicleCurrency, CurrencyCode operationCurrency, DealerExchangeRate? rate, DateTime capturedAtUtc)
    {
        if (vehicleCurrency == operationCurrency)
            return Result.Success<ExchangeRateSnapshot?>(null);

        if (rate is null)
            return Result.Failure<ExchangeRateSnapshot?>(Error.Validation(
                "Currency.ExchangeRateRequired",
                "An exchange rate is required when the operation currency differs from the vehicle currency."));

        bool samePair =
            (rate.BaseCurrency == vehicleCurrency.Value && rate.QuoteCurrency == operationCurrency.Value) ||
            (rate.BaseCurrency == operationCurrency.Value && rate.QuoteCurrency == vehicleCurrency.Value);

        if (!samePair)
            return Result.Failure<ExchangeRateSnapshot?>(Error.Validation(
                "Currency.ExchangeRatePairMismatch",
                "The exchange rate does not match the vehicle and operation currencies."));

        return Result.Success<ExchangeRateSnapshot?>(
            new ExchangeRateSnapshot(rate.BaseCurrency, rate.QuoteCurrency, rate.Rate, capturedAtUtc, rate.Id));
    }
}
