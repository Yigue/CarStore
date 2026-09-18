using Application.Abstractions.Messaging;

namespace Application.Roles.Update;

/// <summary>
/// CFG-03: renames a role and replaces its permission set wholesale — the matrix sends what the
/// role should now be able to do, not a diff.
/// </summary>
public sealed record UpdateRoleCommand(
    Guid RoleId,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions) : ICommand;
