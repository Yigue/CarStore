using Application.Abstractions.Messaging;

namespace Application.Quotes.Create;

// A quote references exactly one of ClientId or LeadId.
public sealed record CreateQuoteCommand(
    Guid CarId,
    Guid? ClientId,
    Guid? LeadId,
    decimal ProposedPrice,
    DateTime ValidUntil,
    string Comments) : ICommand<Guid>;
