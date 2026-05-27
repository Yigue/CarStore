using Application.Abstractions.Messaging;
using Application.Quotes.Get;

namespace Application.Quotes.GetMy;

public sealed record GetMyQuotesQuery() : IQuery<List<QuoteResponse>>;
