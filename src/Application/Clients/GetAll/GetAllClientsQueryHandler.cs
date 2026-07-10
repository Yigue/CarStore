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
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var rawTerm = query.Search.Trim();
            var searchTerm = rawTerm.ToLower();
            Email? emailTerm = TryParseEmail(rawTerm);

            bool isInMemory = context is DbContext dbContext && dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";

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
