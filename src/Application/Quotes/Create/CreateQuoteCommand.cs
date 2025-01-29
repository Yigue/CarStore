using Application.Abstractions.Messaging;

namespace Application.Quotes.Create;

public sealed record CreateQuoteCommand(
    Guid CarId,
    Guid ClientId,
    decimal ProposedPrice,
    DateTime ValidUntil,
    string Comments) : ICommand<Guid>;
