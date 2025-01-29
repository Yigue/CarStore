using Application.Abstractions.Messaging;
using Application.Quotes.Get;

namespace Application.Quotes.GetById;

public sealed record GetQuoteByIdQuery(Guid QuoteId) : IQuery<QuoteResponse>;
