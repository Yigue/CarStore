using Application.Abstractions.Messaging;

namespace Application.Users.Queries.GetRoles;

public sealed record GetRolesQuery : IQuery<RolesResponse>;

public sealed record RolesResponse(IEnumerable<RoleResponse> Roles);