using Application.Abstractions.Messaging;
using SharedKernel;
using System;

namespace Application.Clients.GetAll;

public sealed record GetAllClientsQuery(
    string? Search = null,
    string? Status = null,
    string? Type = null,
    string? Source = null,
    Guid? AssignedAgentId = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    decimal? TotalSalesMin = null,
    decimal? TotalSalesMax = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<ClientResponse>>;
