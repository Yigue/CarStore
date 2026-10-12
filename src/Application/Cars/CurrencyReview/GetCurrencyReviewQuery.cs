using Application.Abstractions.Messaging;

namespace Application.Cars.CurrencyReview;

/// <summary>sfc B0.1.5 / ADR C1: cars whose purchase cost is recorded in a different currency than their price.</summary>
public sealed record GetCurrencyReviewQuery : IQuery<IReadOnlyList<CurrencyReviewItemResponse>>;

/// <summary>One car that needs a currency decision before reports can convert it.</summary>
public sealed record CurrencyReviewItemResponse(
    Guid CarId,
    string PriceCurrency,
    string PurchaseCostCurrency,
    decimal Price,
    decimal PurchaseCost);
