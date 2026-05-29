using Application.Abstractions.Messaging;

namespace Application.Users.Queries.GetPermissions;

public sealed record GetPermissionsQuery : IQuery<PermissionsResponse>;

public sealed record PermissionsResponse(IEnumerable<PermissionResponse> Permissions);