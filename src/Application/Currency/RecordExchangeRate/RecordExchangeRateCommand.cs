using Application.Abstractions.Messaging;

namespace Application.Currency.RecordExchangeRate;

/// <summary>
/// sfc B0.2.3 / D-CURRENCY: the dealer loads a manual rate, meaning 1 <paramref name="BaseCurrency"/> = <paramref name="Rate"/>
/// <paramref name="QuoteCurrency"/>. Append-only; the response is the new rate's id.
/// </summary>
public sealed record RecordExchangeRateCommand(
    string BaseCurrency,
    string QuoteCurrency,
    decimal Rate,
    DateTime EffectiveAtUtc) : ICommand<Guid>;
