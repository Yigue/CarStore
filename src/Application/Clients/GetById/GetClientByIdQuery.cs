using Application.Abstractions.Messaging;
using Application.Clients.GetAll;

namespace Application.Clients.GetById;

public sealed record GetClientByIdQuery(Guid Id) : IQuery<ClientResponse>;
