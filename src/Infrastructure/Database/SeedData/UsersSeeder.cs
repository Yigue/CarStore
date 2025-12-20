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
        if (await context.Users.AnyAsync(u => u.Email == adminEmail, cancellationToken))
        {
            return;
        }

        // Hash de la contraseña
        var passwordHash = passwordHasher.Hash(adminPassword);

        // Crear usuario admin
        var admin = new User(
            adminEmail,
            "Admin",
            "User",
            passwordHash);

        context.Users.Add(admin);
        await context.SaveChangesAsync(cancellationToken);
    }
}

