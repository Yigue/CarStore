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

        string[] permissions = await _context.UserPermissions
            .Where(x => x.UserId == userId)
            .Select(x => x.Permission)
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
