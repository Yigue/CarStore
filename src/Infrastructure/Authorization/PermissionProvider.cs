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
        // Intentar obtener permisos del caché
        var cacheKey = CacheKeys.UserPermissions(userId);
        var cachedPermissions = await _cacheService.GetAsync<HashSet<string>>(cacheKey);
        
        if (cachedPermissions != null)
        {
            _logger.LogDebug("Permissions retrieved from cache for user {UserId}", userId);
            return cachedPermissions;
        }

        // Verificar si el usuario existe
        var userExists = await _context.Users
            .AnyAsync(u => u.Id == userId);

        if (!userExists)
        {
            return [];
        }

        // Por ahora, todos los usuarios autenticados tienen todos los permisos
        // En el futuro, esto debería consultar una tabla de permisos/roles
        // TODO: Implementar sistema de roles y permisos en base de datos
        // TODO: Agregar tabla UserRoles y RolePermissions
        
        var allPermissions = new HashSet<string>
        {
            // Permisos de usuarios
            "users:access",
            // Permisos de autos
            "cars:read",
            "cars:write",
            "cars:delete",
            // Permisos de clientes
            "clients:read",
            "clients:write",
            "clients:delete",
            // Permisos de ventas
            "sales:read",
            "sales:write",
            "sales:delete",
            // Permisos de cotizaciones
            "quotes:read",
            "quotes:write",
            "quotes:delete",
            // Permisos financieros
            "financial:read",
            "financial:write",
            "financial:delete"
        };

        // Guardar en caché
        await _cacheService.SetAsync(cacheKey, allPermissions, CacheTTL.Permissions);
        _logger.LogDebug("Permissions cached for user {UserId} with TTL {TTL}", userId, CacheTTL.Permissions);

        return allPermissions;
    }
}
