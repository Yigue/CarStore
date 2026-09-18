namespace Application.Abstractions.Authorization;

/// <summary>
/// Drops the cached permission set of the users a change affects.
///
/// <para>
/// <c>PermissionProvider</c> caches each user's permissions for thirty minutes, and nothing
/// evicted that cache. Granting a permission, reassigning a role or editing what a role may do
/// therefore appeared to work — the row was written, the screen refreshed — and then did nothing
/// for up to half an hour, which reads as "the system ignored me" and gets the change made twice.
/// Worse in the other direction: a permission REVOKED stayed usable for the rest of the TTL.
/// </para>
///
/// <para>
/// Declared here rather than reaching for <c>ICacheService</c> from a handler: the Application
/// layer states the need, Infrastructure knows it is Redis.
/// </para>
/// </summary>
public interface IPermissionCacheInvalidator
{
    /// <summary>Forgets what one user is allowed to do; the next request reloads from the database.</summary>
    Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets every user holding this role. Editing a role's permissions changes what all of its
    /// holders may do, so invalidating only the editor would leave everyone else on the old set.
    /// </summary>
    Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}
