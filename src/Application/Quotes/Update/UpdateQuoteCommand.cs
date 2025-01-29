using Application.Abstractions.Messaging;
using Domain.Quotes.Attributes;

namespace Application.Quotes.Update;

public sealed record UpdateQuoteCommand(
    Guid Id,
    decimal ProposedPrice,
    DateTime ValidUntil,
    QuoteStatus Status,
    string Comments) : ICommand<Guid>;
