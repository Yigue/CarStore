using Application.Abstractions.Messaging;

namespace Application.Quotes.Accept;

public sealed record AcceptQuoteCommand(Guid QuoteId) : ICommand;

