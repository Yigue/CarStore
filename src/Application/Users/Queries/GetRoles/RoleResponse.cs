namespace Application.Users.Queries.GetRoles;

/// <summary>
/// A role as the roles screen needs it.
///
/// <para>
/// CFG-03 added <see cref="Permissions"/> and <see cref="UserCount"/>: the matrix cannot render
/// what a role may do without the first, and the delete button cannot warn about the people it
/// would strand without the second. <see cref="Id"/> stays a GUID string — it is what
/// <c>CreateUserCommandHandler</c> parses (CFG-01).
/// </para>
/// </summary>
public sealed record RoleResponse(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions,
    int UserCount
);
