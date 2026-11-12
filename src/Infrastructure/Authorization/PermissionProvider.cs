using Application.Abstractions.Data;
using Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Authorization;

internal sealed class PermissionProvider
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PermissionProvider> _logger;

    public PermissionProvider(
        IApplicationDbContext context,
        ICacheService cacheService,
        ILogger<PermissionProvider> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// sfc B6.3 / RN-043 (security review): permissions are now dealer-scoped, not just user-scoped — the
    /// same user can hold a different role per dealer (home <c>User.RoleId</c>, or a
    /// <see cref="Domain.Users.UserDealerMembership"/> role for any other dealer they switched into). The
    /// caller MUST pass the dealer the active token is actually scoped to (its <c>dealer_id</c> claim), or a
    /// switch to a low-privilege dealer would silently keep the high-privilege home role's grants.
    /// </summary>
    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId, Guid dealerId)
    {
        var cacheKey = CacheKeys.UserPermissions(userId, dealerId);
        var cachedPermissions = await _cacheService.GetAsync<HashSet<string>>(cacheKey);

        if (cachedPermissions is not null)
        {
            _logger.LogDebug(
                "Permissions retrieved from cache for user {UserId} on dealer {DealerId}. Count: {Count}",
                userId, dealerId, cachedPermissions.Count);
            return cachedPermissions;
        }

        var homeUser = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new { u.DealerId, u.RoleId })
            .FirstOrDefaultAsync();

        var permissionsSet = new HashSet<string>();

        if (homeUser is not null)
        {
            // Effective role for THIS dealer: the home role only when dealerId is the home dealer,
            // otherwise the membership this user was explicitly granted for it — never a fallback to
            // the home role, which is exactly the privilege-escalation this check closes.
            Guid? effectiveRoleId = dealerId == homeUser.DealerId
                ? homeUser.RoleId
                : await _context.UserDealerMemberships
                    .Where(m => m.UserId == userId && m.DealerId == dealerId)
                    .Select(m => (Guid?)m.RoleId)
                    .FirstOrDefaultAsync();

            if (effectiveRoleId is { } roleId && roleId != Guid.Empty)
            {
                string[] rolePermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleId == roleId)
                    .Select(rp => rp.Permission)
                    .ToArrayAsync();

                permissionsSet.UnionWith(rolePermissions);
            }
        }

        // Platform-level grants (e.g. SuperAdmin's platform:* permissions) are not dealer-scoped at all.
        string[] userPermissions = await _context.UserPermissions
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission)
            .ToArrayAsync();

        permissionsSet.UnionWith(userPermissions);

        // Only cache when there are permissions. Caching an empty set locks the
        // user out for the full TTL even after the missing rows are added.
        if (permissionsSet.Count > 0)
        {
            await _cacheService.SetAsync(cacheKey, permissionsSet, CacheTTL.Permissions);
            _logger.LogDebug(
                "Permissions loaded from DB and cached for user {UserId} on dealer {DealerId}. Count: {Count}",
                userId, dealerId, permissionsSet.Count);
        }
        else
        {
            _logger.LogWarning(
                "No permissions found in DB for user {UserId} on dealer {DealerId} — not caching to allow recovery once permissions are seeded.",
                userId, dealerId);
        }

        return permissionsSet;
    }
}
