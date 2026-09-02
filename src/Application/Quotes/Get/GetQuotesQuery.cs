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
/// <param name="CarId">
/// Narrows to every quote raised for one vehicle. Several buyers can hold competing offers on
/// the same unit, so whoever is about to price it needs to see what the others were offered —
/// otherwise the second quote is written blind against the first.
/// </param>
public sealed record GetQuotesQuery(
    Guid? ClientId = null,
    Guid? LeadId = null,
    Guid? CarId = null) : IQuery<List<QuoteResponse>>;
