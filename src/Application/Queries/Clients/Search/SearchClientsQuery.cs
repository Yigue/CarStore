using Application.Abstractions.Messaging;
using Application.Clients.GetAll;

namespace Application.Queries.Clients.Search;

public sealed class SearchClientsQuery : IQuery<IEnumerable<ClientResponse>>
{
    public string SearchTerm { get; set; } = string.Empty;
}