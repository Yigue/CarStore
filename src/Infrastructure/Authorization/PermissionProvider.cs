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

    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId)
    {
        var cacheKey = CacheKeys.UserPermissions(userId);
        var cachedPermissions = await _cacheService.GetAsync<HashSet<string>>(cacheKey);

        if (cachedPermissions is not null)
        {
            _logger.LogDebug("Permissions retrieved from cache for user {UserId}. Count: {Count}", userId, cachedPermissions.Count);
            return cachedPermissions;
        }

        // First find the user's role ID, then find all permissions for that role.
        var roleId = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.RoleId)
            .FirstOrDefaultAsync();

        if (roleId == Guid.Empty)
        {
            var userPermissions = await _context.UserPermissions
                .Where(up => up.UserId == userId)
                .Select(up => up.Permission)
                .ToArrayAsync();
                
            var userPermSet = userPermissions.ToHashSet();
            if (userPermSet.Count > 0)
            {
                await _cacheService.SetAsync(cacheKey, userPermSet, CacheTTL.Permissions);
                _logger.LogDebug("UserPermissions loaded from DB and cached for user {UserId}. Count: {Count}", userId, userPermSet.Count);
            }
            return userPermSet;
        }

        string[] permissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission)
            .ToArrayAsync();

        var permissionsSet = permissions.ToHashSet();

        // Only cache when there are permissions. Caching an empty set locks the
        // user out for the full TTL even after the missing rows are added.
        if (permissionsSet.Count > 0)
        {
            await _cacheService.SetAsync(cacheKey, permissionsSet, CacheTTL.Permissions);
            _logger.LogDebug("Permissions loaded from DB and cached for user {UserId}. Count: {Count}", userId, permissionsSet.Count);
        }
        else
        {
            _logger.LogWarning("No permissions found in DB for user {UserId} — not caching to allow recovery once permissions are seeded.", userId);
        }

        return permissionsSet;
    }
}
