using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.Projections;
using Application.Clients;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Sales.Attributes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Clients.GetAll;

internal sealed class GetAllClientsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllClientsQuery, PaginatedResult<ClientResponse>>
{
    public async Task<Result<PaginatedResult<ClientResponse>>> Handle(
        GetAllClientsQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = context.Clients
            .AsNoTracking()
            .Include(c => c.Sales)
            .AsQueryable();

        // 1. Advanced Filters
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ClientStatus>(query.Status, true, out var statusVal))
        {
            dbQuery = dbQuery.Where(c => c.Status == statusVal);
        }

        if (!string.IsNullOrWhiteSpace(query.Type) && Enum.TryParse<ClientType>(query.Type, true, out var typeVal))
        {
            dbQuery = dbQuery.Where(c => c.Type == typeVal);
        }

        if (!string.IsNullOrWhiteSpace(query.Source) && Enum.TryParse<AcquisitionSource>(query.Source, true, out var sourceVal))
        {
            dbQuery = dbQuery.Where(c => c.AcquisitionSource == sourceVal);
        }

        if (query.AssignedAgentId.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.AssignedAgentId == query.AssignedAgentId.Value);
        }

        if (query.CreatedFrom.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.CreatedAt <= query.CreatedTo.Value);
        }

        if (query.TotalSalesMin.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.Sales
                .Where(s => s.Status == SaleStatus.Completed)
                .Sum(s => s.FinalPrice.Amount) >= query.TotalSalesMin.Value);
        }

        if (query.TotalSalesMax.HasValue)
        {
            dbQuery = dbQuery.Where(c => c.Sales
                .Where(s => s.Status == SaleStatus.Completed)
                .Sum(s => s.FinalPrice.Amount) <= query.TotalSalesMax.Value);
        }

        // 2. Search on fullName (case & accent insensitive) or exact email
        //
        // qa-p0-blockers C1 (D1 superseded, decision 2026-08-03): D1's
        // EF.Functions.Collate(..., "und-u-ks-primary").Contains(...) is architecturally
        // impossible -- confirmed live against postgres:17-alpine that PostgreSQL rejects
        // pattern-matching (LIKE) against a nondeterministic ICU collation with
        // "0A000: nondeterministic collations are not supported for LIKE" (a permanent
        // restriction, not a PG-version floor). The replacement filters and paginates in SQL
        // via the `search_name` STORED generated column (`clients.search_name`, migration
        // `AddClientSearchNameColumn`): `lower(f_unaccent(first_name || ' ' || last_name))`,
        // pinned to a deterministic "C" collation so it can never regress into the same
        // 0A000 error, backed by a GIN trigram index for `LIKE`.
        //
        // Provider branch, explicit and justified (trap 3): the InMemory provider (unit
        // tests only) has no generated column -- `search_name` is never computed there. For
        // InMemory, filtering falls back to the original in-process RemoveAccents predicate,
        // which InMemory's LINQ-to-Objects execution can evaluate directly. For every
        // relational provider (Postgres in production and in the Postgres integration
        // suite), the predicate is pushed down as `search_name LIKE '%' || lower(f_unaccent(@term))
        // || '%'` -- the TERM is unaccented via the same Postgres `f_unaccent` dictionary
        // (mapped as an EF DbFunction) that produced the column, so .NET FormD normalization
        // and PostgreSQL's unaccent dictionary can never disagree on edge cases (ß, ø, Đ).
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var rawTerm = query.Search.Trim();
            Email? emailTerm = TryParseEmail(rawTerm);
            // Positive Npgsql check, not a negative InMemory one: `search_name` and f_unaccent
            // exist only under Postgres, so every other provider (SQLite, InMemory) must take
            // the in-process path.
            var isNpgsql = context is DbContext db && db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";

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

            int searchTotalCount = await dbQuery.CountAsync(cancellationToken);
            var searchPage = query.Page > 0 ? query.Page : 1;
            var searchPageSize = query.PageSize > 0 ? query.PageSize : 20;

            var searchClients = await dbQuery
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .Skip((searchPage - 1) * searchPageSize)
                .Take(searchPageSize)
                .ToListAsync(cancellationToken);

            var searchItems = searchClients.Select(ClientResponseMapper.Map).ToList();

            var searchPaginatedResult = new PaginatedResult<ClientResponse>(
                searchItems,
                searchTotalCount,
                searchPage,
                searchPageSize);

            return Result.Success(searchPaginatedResult);
        }

        int totalCount = await dbQuery.CountAsync(cancellationToken);

        // Apply pagination
        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize > 0 ? query.PageSize : 20;

        var clients = await dbQuery
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = clients.Select(ClientResponseMapper.Map).ToList();

        var paginatedResult = new PaginatedResult<ClientResponse>(
            items,
            totalCount,
            page,
            pageSize);

        return Result.Success(paginatedResult);
    }

    private static Email? TryParseEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

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
