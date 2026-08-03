using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Clients.Projections;
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
        // NOTE (qa-p0-blockers C1, 2026-08-03): this is intentionally NOT using
        // EF.Functions.Collate(..., "und-u-ks-primary") against a real Postgres provider.
        // Verified via a live postgres:17-alpine container that PostgreSQL rejects
        // pattern-matching (LIKE) against a non-deterministic ICU collation with
        // "0A000: nondeterministic collations are not supported for LIKE" -- this is not a
        // PG-version floor as design D1 assumed, it is a permanent PostgreSQL restriction:
        // non-deterministic collations only support equality/ordering, never LIKE/Contains.
        // The `AddUndKsPrimaryCollation` migration and collation object are real and correct;
        // they just cannot back a substring search via EF's `.Contains()` translation, which
        // always compiles to `... COLLATE "und-u-ks-primary" LIKE '%...%'`.
        // This full-table-load + in-process filter is a known, undocumented-until-now
        // performance regression (unbounded materialization before pagination) -- flagged by
        // sdd-verify as CRITICAL C1. Fixing it requires a design decision that supersedes D1
        // (e.g. a generated column `lower(unaccent(...))` + expression index) which was
        // explicitly out of scope for this apply batch and is documented back to the
        // orchestrator rather than silently implemented here.
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var rawTerm = query.Search.Trim();
            var searchTerm = rawTerm.ToLower();
            Email? emailTerm = TryParseEmail(rawTerm);

            var normalizedSearch = RemoveAccents(searchTerm).ToLowerInvariant();
            var allList = await dbQuery.ToListAsync(cancellationToken);
            var filteredList = allList.Where(c =>
                RemoveAccents(c.FirstName + " " + c.LastName).ToLowerInvariant().Contains(normalizedSearch) ||
                (emailTerm != null && c.Email == emailTerm)).ToList();

            int searchTotalCount = filteredList.Count;
            var searchPage = query.Page > 0 ? query.Page : 1;
            var searchPageSize = query.PageSize > 0 ? query.PageSize : 20;

            var searchClients = filteredList
                .OrderBy(c => c.LastName)
                .ThenBy(c => c.FirstName)
                .Skip((searchPage - 1) * searchPageSize)
                .Take(searchPageSize)
                .ToList();

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
