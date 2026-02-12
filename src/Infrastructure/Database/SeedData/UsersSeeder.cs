using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// Seeder para Usuarios del sistema.
/// </summary>
internal static class UsersSeeder
{
    /// <summary>
    /// Seedea el usuario administrador por defecto.
    /// </summary>
    public static async Task SeedAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        const string adminEmail = "admin@carstore.com";
        const string adminPassword = "Admin123!"; // En producción, usar variable de entorno

        // Verificar si ya existe el usuario admin
        var admin = await context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail, cancellationToken);
        
        if (admin is null)
        {
            // Hash de la contraseña
            var passwordHash = passwordHasher.Hash(adminPassword);

            // Crear usuario admin
            admin = new User(
                adminEmail,
                "Admin",
                "User",
                passwordHash);

            context.Users.Add(admin);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Seeder de permisos para el admin
        if (!await context.UserPermissions.AnyAsync(p => p.UserId == admin.Id, cancellationToken))
        {
            var permissions = new List<string>
            {
                "cars:read", "cars:create", "cars:update", "cars:delete",
                "clients:read", "clients:create", "clients:update", "clients:delete",
                "sales:read", "sales:create", "sales:update", "sales:delete",
                "quotes:read", "quotes:create", "quotes:update", "quotes:delete",
                "financial:read", "financial:create", "financial:update", "financial:delete",
                "users:read"
            };

            foreach (var permission in permissions)
            {
                context.UserPermissions.Add(new UserPermission(admin.Id, permission));
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}

