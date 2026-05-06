using Application.Abstractions.Messaging;
using Application.Queries.Clients.GetTop;

namespace Application.Queries.Clients.GetTop;

public sealed class GetTopClientsQuery : IQuery<IEnumerable<ClientWithRevenueResponse>>
{
    public int Limit { get; set; } = 10;
}