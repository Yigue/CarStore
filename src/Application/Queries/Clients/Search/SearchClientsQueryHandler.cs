using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients;
using Application.Clients.GetAll;
using Application.Clients.Projections;
using Domain.Clients;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Queries.Clients.Search;

internal sealed class SearchClientsQueryHandler
    : IQueryHandler<SearchClientsQuery, IEnumerable<ClientResponse>>
{
    private readonly IApplicationDbContext _context;

    public SearchClientsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IEnumerable<ClientResponse>>> Handle(
        SearchClientsQuery query,
        CancellationToken cancellationToken)
    {
        var rawTerm = query.SearchTerm ?? string.Empty;
        Email? emailTerm = TryParseEmail(rawTerm);

        var dbQuery = _context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .AsQueryable();

        // qa-p0-blockers C1 (D1 superseded, decision 2026-08-03): see the matching note in
        // GetAllClientsQueryHandler.Handle for the full rationale. Filtering and the 50-row
        // cap now execute in SQL via the `search_name` STORED generated column instead of an
        // unbounded full-table load followed by in-process filtering.
        if (rawTerm != string.Empty)
        {
            // Positive Npgsql check, not a negative InMemory one: `search_name` and f_unaccent
            // exist only under Postgres, so every other provider (SQLite, InMemory) must take
            // the in-process path.
            var isNpgsql = _context is DbContext db && db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

            if (!isNpgsql)
            {
                var normalizedSearch = RemoveAccents(rawTerm.ToLower()).ToLowerInvariant();
                dbQuery = dbQuery.Where(c =>
                    RemoveAccents(c.FirstName + " " + c.LastName).ToLowerInvariant().Contains(normalizedSearch) ||
                    (emailTerm != null && c.Email == emailTerm));
            }
            else
            {
                // Unaccent MUST stay inside the expression tree so EF translates it to
                // `f_unaccent(@term)`. Hoisting it to a local evaluates it in .NET and throws.
                dbQuery = dbQuery.Where(c =>
                    EF.Property<string>(c, "SearchName")
                        .Contains(ClientSearchFunctions.Unaccent(rawTerm).ToLower()) ||
                    (emailTerm != null && c.Email == emailTerm));
            }
        }

        List<Client> clients = await dbQuery
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Take(50)
            .ToListAsync(cancellationToken);

        IEnumerable<ClientResponse> responses = clients.Select(ClientResponseMapper.Map);
        return Result.Success<IEnumerable<ClientResponse>>(responses);
    }

    private static Email? TryParseEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return new Email(value);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static string RemoveAccents(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        return new string(text
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray())
            .Normalize(System.Text.NormalizationForm.FormC);
    }
}