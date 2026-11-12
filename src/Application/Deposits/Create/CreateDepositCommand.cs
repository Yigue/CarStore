using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;

namespace Application.Deposits.Create;

/// <summary>
/// sfc B1.3.5 / RN-001: a seña over a car, for a client or a lead, optionally tied to the accepted quote that reserved
/// the car. <paramref name="Currency"/> is required (D-CURRENCY); a currency different from the car's needs the dealer's
/// manual rate. <paramref name="PaymentMethod"/> describes how the money was received (income transaction).
/// </summary>
public sealed record CreateDepositCommand(
    Guid CarId,
    Guid? ClientId,
    Guid? LeadId,
    Guid? QuoteId,
    decimal Amount,
    string Currency,
    PaymentMethod PaymentMethod) : ICommand<Guid>;
