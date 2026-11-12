using Application.Abstractions.Messaging;

namespace Application.Deposits.Get;

/// <summary>sfc B1.3.5: the caller's deposits, optionally for one car, newest first.</summary>
public sealed record GetDepositsQuery(Guid? CarId = null) : IQuery<IReadOnlyList<DepositResponse>>;

public sealed record DepositResponse(
    Guid Id,
    Guid CarId,
    Guid? ClientId,
    Guid? LeadId,
    Guid? QuoteId,
    decimal Amount,
    string Currency,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    decimal? RefundedAmount,
    string? RefundReason,
    Guid? AppliedSaleId,
    decimal? ExchangeRate);
