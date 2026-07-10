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

        if (searchTerm != string.Empty)
        {
            bool isInMemory = _context is DbContext dbContext && dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

            if (isInMemory)
            {
                var normalizedSearch = RemoveAccents(searchTerm).ToLowerInvariant();
                dbQuery = dbQuery.Where(c => 
                    RemoveAccents(c.FirstName + " " + c.LastName).ToLower().Contains(normalizedSearch) ||
                    (emailTerm != null && c.Email == emailTerm));
            }
            else
            {
                var normalizedSearch = RemoveAccents(searchTerm);
                dbQuery = dbQuery.Where(c => 
                    EF.Functions.Collate(c.FirstName + " " + c.LastName, "und-u-ks-primary").Contains(normalizedSearch) ||
                    (emailTerm != null && c.Email == emailTerm));
            }
        }

        List<Client> clients = await dbQuery
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