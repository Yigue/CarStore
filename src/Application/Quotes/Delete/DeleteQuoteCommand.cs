using Application.Abstractions.Messaging;

namespace Application.Quotes.Delete;

public sealed record DeleteQuoteCommand(Guid QuoteId) : ICommand;

