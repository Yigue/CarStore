using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
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
        var searchTerm = rawTerm.ToLower();

        Email? emailTerm = TryParseEmail(rawTerm);

        var dbQuery = _context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .AsQueryable();

        // NOTE (qa-p0-blockers C1, 2026-08-03): see the matching note in
        // GetAllClientsQueryHandler.Handle -- EF.Functions.Collate(..., "und-u-ks-primary")
        // cannot back a substring search against real Postgres; nondeterministic ICU
        // collations do not support LIKE (confirmed live against postgres:17-alpine:
        // "0A000: nondeterministic collations are not supported for LIKE"). This full-table
        // load is a known, flagged (sdd-verify CRITICAL C1) performance regression pending a
        // design decision that supersedes D1.
        List<Client> clients = await dbQuery.ToListAsync(cancellationToken);

        if (searchTerm != string.Empty)
        {
            var normalizedSearch = RemoveAccents(searchTerm).ToLowerInvariant();
            clients = clients.Where(c =>
                RemoveAccents(c.FirstName + " " + c.LastName).ToLowerInvariant().Contains(normalizedSearch) ||
                (emailTerm != null && c.Email == emailTerm)).ToList();
        }

        clients = clients.Take(50).ToList();

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