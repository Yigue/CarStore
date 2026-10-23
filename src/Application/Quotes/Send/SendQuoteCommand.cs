using Application.Abstractions.Messaging;

namespace Application.Quotes.Send;

/// <summary>
/// sfc B4.3 / B10 / RN-018: renders the quote's proposal PDF, uploads it, emails
/// <see cref="Application.Abstractions.Email.EmailTemplate.QuoteSent"/> to the quote's client and
/// records a <c>LeadActivity</c> entry when the quote (or its client) traces back to a lead.
/// </summary>
public sealed record SendQuoteCommand(Guid QuoteId) : ICommand<SendQuoteResult>;
