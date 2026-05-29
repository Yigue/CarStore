using Application.Abstractions.Messaging;

namespace Application.Users.Queries.GetUserPermissions;

public sealed record GetUserPermissionsQuery(Guid UserId) : IQuery<UserPermissionsResponse>;

public sealed record UserPermissionsResponse(Guid UserId, IEnumerable<string> Permissions);