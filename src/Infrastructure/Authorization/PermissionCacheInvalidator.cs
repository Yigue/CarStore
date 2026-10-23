using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

/// <inheritdoc />
internal sealed class PermissionCacheInvalidator(
    IApplicationDbContext context,
    ICacheService cacheService)
    : IPermissionCacheInvalidator
{
    // sfc B6.3: the cache key is now (userId, dealerId) — the same user can hold a different role, and
    // therefore different permissions, per dealer. RemoveByPatternAsync is not implemented for Redis
    // (see RedisCacheService), so every dealer this user could be scoped to is invalidated explicitly.
    public async Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        foreach (Guid dealerId in await DealerIdsOfAsync(userId, cancellationToken))
        {
            await cacheService.RemoveAsync(CacheKeys.UserPermissions(userId, dealerId), cancellationToken);
        }
    }

    public async Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: role administration runs inside a tenant, but this also has to work
        // from a background or provisioning path where no tenant is resolved. A role belongs to exactly
        // one dealer (Role.DealerId) — every holder's cache entry for THAT dealer is what needs clearing,
        // whether they hold it as their home role or as a UserDealerMembership grant.
        Guid? roleDealerId = await context.Roles
            .IgnoreQueryFilters()
            .Where(r => r.Id == roleId)
            .Select(r => (Guid?)r.DealerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleDealerId is null)
        {
            return;
        }

        List<Guid> homeHolders = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.RoleId == roleId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        List<Guid> membershipHolders = await context.UserDealerMemberships
            .IgnoreQueryFilters()
            .Where(m => m.RoleId == roleId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        foreach (Guid userId in homeHolders.Concat(membershipHolders).Distinct())
        {
            await cacheService.RemoveAsync(CacheKeys.UserPermissions(userId, roleDealerId.Value), cancellationToken);
        }
    }

    private async Task<List<Guid>> DealerIdsOfAsync(Guid userId, CancellationToken cancellationToken)
    {
        Guid? homeDealerId = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => (Guid?)u.DealerId)
            .FirstOrDefaultAsync(cancellationToken);

        List<Guid> extraDealerIds = await context.UserDealerMemberships
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId)
            .Select(m => m.DealerId)
            .ToListAsync(cancellationToken);

        return homeDealerId is { } home ? extraDealerIds.Append(home).Distinct().ToList() : extraDealerIds;
    }
}
