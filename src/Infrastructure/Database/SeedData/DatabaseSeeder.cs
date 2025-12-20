using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Cars.Atribbutes;
using Domain.Financial.Attributes;
using Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// Seeder principal para datos iniciales de la base de datos.
/// Ejecuta seeders específicos para cada entidad.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Ejecuta todos los seeders si la base de datos está vacía.
    /// </summary>
    public static async Task SeedAsync(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        // Verificar si ya hay datos seedeados
        if (await context.Marca.AnyAsync(cancellationToken))
        {
            return; // Ya seedeado, no duplicar
        }

        // Ejecutar seeders en orden
        await BrandsSeeder.SeedAsync(context, cancellationToken);
        await TransactionCategoriesSeeder.SeedAsync(context, cancellationToken);
        await UsersSeeder.SeedAsync(context, passwordHasher, cancellationToken);

        // Guardar todos los cambios
        await context.SaveChangesAsync(cancellationToken);
    }
}

