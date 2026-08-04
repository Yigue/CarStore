using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Cars.Attributes;
using Domain.Financial.Attributes;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // Users are reconciled on every startup: UsersSeeder is idempotent and
        // self-heals stale roles, so it must run even when the bulk seed data
        // already exists (the Marca guard below would otherwise skip it).
        await UsersSeeder.SeedAsync(context, passwordHasher, configuration, logger, cancellationToken);

        // 2. Reference data (Brands & Categories)
        if (!await context.Marca.AnyAsync(cancellationToken))
        {
            await BrandsSeeder.SeedAsync(context, cancellationToken);
            await TransactionCategoriesSeeder.SeedAsync(context, cancellationToken);
        }

        // 3. DealerSettings
        if (context is ApplicationDbContext dbContext)
        {
            await DealerSettingsSeeder.SeedAsync(dbContext, cancellationToken);
        }

        // 4. DevData (Clients, Cars, Sales, Quotes, Leads, Subscriptions)
        await DevDataSeeder.SeedAsync(context, cancellationToken);

        // Guardar todos los cambios
        await context.SaveChangesAsync(cancellationToken);
    }
}

