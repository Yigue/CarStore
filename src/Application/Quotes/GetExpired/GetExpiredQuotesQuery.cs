using Application.Abstractions.Messaging;
using Application.Quotes.Get;

namespace Application.Quotes.GetExpired;

public sealed record GetExpiredQuotesQuery : IQuery<List<QuoteResponse>>;

