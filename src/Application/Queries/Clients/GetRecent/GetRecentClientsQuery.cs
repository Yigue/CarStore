using Application.Abstractions.Messaging;
using Application.Clients.GetAll;

namespace Application.Queries.Clients.GetRecent;

public sealed class GetRecentClientsQuery : IQuery<IEnumerable<ClientResponse>>
{
    public int Limit { get; set; } = 10;
}