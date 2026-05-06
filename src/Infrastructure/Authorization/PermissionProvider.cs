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
        // 1. Intentar obtener permisos del caché
        var cacheKey = CacheKeys.UserPermissions(userId);
        var cachedPermissions = await _cacheService.GetAsync<HashSet<string>>(cacheKey);
        
        if (cachedPermissions is not null)
        {
            _logger.LogDebug("Permissions retrieved from cache for user {UserId}", userId);
            return cachedPermissions;
        }

        // 2. Si no estÃ¡ en cachÃ©, buscar en base de datos
        // Tip: Usamos AsNoTracking para mejor rendimiento en lecturas
        var query = _context.UserPermissions
            .Where(x => x.UserId == userId)
            .Select(x => x.Permission);

        string[] permissions;
        if (query is IAsyncEnumerable<string>)
        {
            permissions = await query.ToArrayAsync();
        }
        else
        {
            permissions = query.ToArray();
        }
            
        var permissionsSet = permissions.ToHashSet();

        // 3. Guardar en caché (incluso si está vacío, para evitar golpear la BD repetidamente)
        await _cacheService.SetAsync(cacheKey, permissionsSet, CacheTTL.Permissions);
        
        _logger.LogDebug("Permissions loaded from DB and cached for user {UserId}. Count: {Count}", userId, permissionsSet.Count);

        return permissionsSet;
    }
}
