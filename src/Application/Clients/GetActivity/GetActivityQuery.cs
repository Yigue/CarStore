using Application.Abstractions.Messaging;

namespace Application.Clients.GetActivity;

public sealed record GetActivityQuery(Guid ClientId, int Page = 1, int PageSize = 50) : IQuery<ClientActivityResponse>;
