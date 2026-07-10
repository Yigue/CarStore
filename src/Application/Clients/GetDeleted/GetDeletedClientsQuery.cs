using Application.Abstractions.Messaging;
using Application.Clients.GetAll;
using SharedKernel;

namespace Application.Clients.GetDeleted;

public sealed record GetDeletedClientsQuery(
    int Page = 1,
    int PageSize = 20) : IQuery<PaginatedResult<ClientResponse>>;
