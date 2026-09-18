using SharedKernel;

namespace Domain.Users;

/// <summary>CFG-03: what can go wrong when a dealership builds its own roles.</summary>
public static class RoleErrors
{
    public static Error NotFound(Guid roleId) => Error.NotFound(
        "Roles.NotFound",
        $"The role with Id = '{roleId}' was not found.");

    public static Error NameAlreadyExists(string name) => Error.Conflict(
        "Roles.NameAlreadyExists",
        $"This dealership already has a role named '{name}'.");

    /// <summary>
    /// A permission the API never checks is a checkbox that promises an access it cannot grant.
    /// The catalogue is the list of everything the rules actually ask for.
    /// </summary>
    public static Error PermissionNotGrantable(string permission) => Error.Validation(
        "Roles.PermissionNotGrantable",
        $"'{permission}' is not a permission this system enforces. Call GET /api/v1/permissions for the ones that are.");

    /// <summary>
    /// Deleting a role out from under its holders would leave them with no permissions at all —
    /// locked out of a system they were using a second ago, with nothing saying why.
    /// </summary>
    public static Error StillAssigned(Guid roleId, int userCount) => Error.Conflict(
        "Roles.StillAssigned",
        $"The role with Id = '{roleId}' still has {userCount} user(s). Move them to another role before deleting it.");

    /// <summary>
    /// The lock-out guard. A dealership that removes the last role able to administer roles can
    /// never grant that permission again — no screen reachable by anyone left can do it, and
    /// recovering means someone with database access.
    /// </summary>
    public static Error WouldRemoveLastAdministrator() => Error.Conflict(
        "Roles.WouldRemoveLastAdministrator",
        "This is the only role left that can administer roles. Removing that permission would lock the dealership out of its own configuration.");
}
