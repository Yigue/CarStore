using Application.Abstractions.Messaging;

namespace Application.Quotes.Get;

public sealed record GetQuotesQuery : IQuery<List<QuoteResponse>>;
