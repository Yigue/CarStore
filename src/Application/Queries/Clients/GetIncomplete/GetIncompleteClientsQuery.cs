using Application.Abstractions.Messaging;

namespace Application.Queries.Clients.GetIncomplete;

public sealed record GetIncompleteClientsQuery : IQuery<IEnumerable<IncompleteClientResponse>>;
