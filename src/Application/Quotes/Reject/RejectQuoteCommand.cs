using Application.Abstractions.Messaging;

namespace Application.Quotes.Reject;

public sealed record RejectQuoteCommand(
    Guid QuoteId,
    string Reason) : ICommand;

