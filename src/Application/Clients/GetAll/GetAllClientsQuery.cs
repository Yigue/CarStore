using Application.Abstractions.Messaging;

namespace Application.Clients.GetAll;

public sealed record GetAllClientsQuery : IQuery<IReadOnlyList<ClientResponse>>;
