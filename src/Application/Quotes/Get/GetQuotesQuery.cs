using Application.Abstractions.Messaging;

namespace Application.Quotes.Get;

/// <summary>
/// All quotes for the tenant, optionally narrowed to one party.
/// <para>
/// The handler filtered by nothing, which is why the client detail screen shipped a hardcoded
/// "No hay cotizaciones para este cliente" — there was no way to ask for one client's quotes, so
/// the tab was a placeholder that answered the same thing whether or not any existed.
/// </para>
/// </summary>
public sealed record GetQuotesQuery(
    Guid? ClientId = null,
    Guid? LeadId = null) : IQuery<List<QuoteResponse>>;
