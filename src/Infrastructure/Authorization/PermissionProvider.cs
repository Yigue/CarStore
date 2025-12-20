using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

internal sealed class PermissionProvider
{
    private readonly IApplicationDbContext _context;

    public PermissionProvider(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HashSet<string>> GetForUserIdAsync(Guid userId)
    {
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
        // TODO: Implementar caché de permisos para mejorar rendimiento
        
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

        return allPermissions;
    }
}
