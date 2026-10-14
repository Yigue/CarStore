using Application.Abstractions.Messaging;
using Domain.Quotes.Attributes;

namespace Application.Quotes.Create;

// A quote references exactly one of ClientId or LeadId.
public sealed record CreateQuoteCommand(
    Guid CarId,
    Guid? ClientId,
    Guid? LeadId,
    decimal ProposedPrice,
    PaymentMethod PaymentMethod,
    DateTime ValidUntil,
    string Comments,
    // sfc B0.2.3 phase 1: optional price currency; ARS when omitted.
    string? Currency = null) : ICommand<Guid>;
