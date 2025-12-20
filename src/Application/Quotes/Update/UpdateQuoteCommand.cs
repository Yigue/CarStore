using Application.Abstractions.Messaging;

namespace Application.Quotes.Update;

public sealed record UpdateQuoteCommand(
    Guid Id,
    decimal ProposedPrice,
    DateTime ValidUntil,
    string Comments) : ICommand<Guid>;
