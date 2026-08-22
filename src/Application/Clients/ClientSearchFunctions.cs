using System;

namespace Application.Clients;

/// <summary>
/// qa-p0-blockers C1 (D1 superseded, decision 2026-08-03). Marker for the EF Core <c>DbFunction</c>
/// mapping of the Postgres <c>public.f_unaccent(text)</c> SQL wrapper created by the
/// <c>AddClientSearchNameColumn</c> migration. Registered against this <see cref="Unaccent"/> method
/// via <c>modelBuilder.HasDbFunction(...)</c> in <c>Infrastructure.Database.ApplicationDbContext</c>.
///
/// Lives in Application (not Infrastructure) so query handlers can reference it directly without
/// creating an Application → Infrastructure dependency; Infrastructure already depends on
/// Application and supplies the actual SQL translation via model configuration.
///
/// Term/column symmetry trap: the search TERM must be unaccented by the same Postgres
/// <c>unaccent</c> dictionary that produced the stored <c>search_name</c> column, not by .NET
/// <see cref="System.Globalization.CharUnicodeInfo"/> normalization. <c>FormD</c> decomposition and
/// PostgreSQL's unaccent dictionary disagree on some characters (e.g. ß, ø, Đ do not decompose to an
/// ASCII base + combining mark under Unicode NFD), so normalizing the term in .NET while the column is
/// normalized in Postgres would silently miss rows. Mapping this method as a translated
/// <c>DbFunction</c> forces the term through the exact same SQL-side function, so both sides of the
/// <c>LIKE</c> predicate agree by construction.
///
/// This method body is never executed in .NET — EF Core translates calls to it into
/// <c>f_unaccent(...)</c> in the generated SQL. It only participates in relational (Postgres) query
/// translation; the InMemory provider branch never references it (see
/// <c>GetAllClientsQueryHandler</c>/<c>SearchClientsQueryHandler</c>).
///
/// WARNING: the call MUST appear inside the LINQ expression tree, e.g.
/// <c>.Where(c =&gt; EF.Property&lt;string&gt;(c, "SearchName").Contains(Unaccent(term).ToLower()))</c>.
/// Hoisting it to a local first (<c>var t = Unaccent(term);</c>) is ordinary eager C#, not an
/// expression tree — EF never sees it and this method throws at runtime.
/// </summary>
public static class ClientSearchFunctions
{
    public static string Unaccent(string value) =>
        throw new NotSupportedException($"{nameof(Unaccent)} can only be used within a LINQ query translated to SQL.");
}
