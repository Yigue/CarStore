namespace Application.Users.Queries.GetPermissions;

/// <summary>
/// One grantable permission. <paramref name="Id"/> is the exact string the endpoints check — the
/// client sends it straight back when granting, so it must never be prettified on the way out.
/// </summary>
public sealed record PermissionResponse(
    string Id,
    string Name,
    string Module
);
