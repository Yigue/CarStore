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
    string Comments) : ICommand<Guid>;
