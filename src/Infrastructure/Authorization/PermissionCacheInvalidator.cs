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
    public Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        cacheService.RemoveAsync(CacheKeys.UserPermissions(userId), cancellationToken);

    public async Task InvalidateRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: role administration runs inside a tenant, but this also has to work
        // from a background or provisioning path where no tenant is resolved. The roleId is
        // already dealer-scoped by whoever loaded the role, so widening the lookup cannot leak —
        // it only finds the users that actually hold this role.
        List<Guid> userIds = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.RoleId == roleId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (Guid userId in userIds)
        {
            await cacheService.RemoveAsync(CacheKeys.UserPermissions(userId), cancellationToken);
        }
    }
}
