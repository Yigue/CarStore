using Application.Abstractions.Data;
using Domain.Cars.Atribbutes;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// Seeder para Marcas y Modelos de vehículos.
/// </summary>
internal static class BrandsSeeder
{
    /// <summary>
    /// Seedea las marcas y modelos principales según el roadmap.
    /// </summary>
    public static async Task SeedAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Verificar si ya hay marcas
        if (await context.Marca.AnyAsync(cancellationToken))
        {
            return;
        }

        var marcas = new List<Marca>
        {
            new("Toyota"),
            new("Ford"),
            new("Chevrolet"),
            new("Volkswagen")
        };

        context.Marca.AddRange(marcas);
        await context.SaveChangesAsync(cancellationToken);

        // Crear modelos para cada marca
        var modelos = new List<Modelo>();

        // Toyota
        var toyota = marcas.First(m => m.Nombre == "Toyota");
        modelos.AddRange(new[]
        {
            new Modelo("Corolla", toyota.Id),
            new Modelo("Camry", toyota.Id),
            new Modelo("RAV4", toyota.Id),
            new Modelo("Hilux", toyota.Id)
        });

        // Ford
        var ford = marcas.First(m => m.Nombre == "Ford");
        modelos.AddRange(new[]
        {
            new Modelo("Fiesta", ford.Id),
            new Modelo("Focus", ford.Id),
            new Modelo("Mustang", ford.Id),
            new Modelo("Ranger", ford.Id)
        });

        // Chevrolet
        var chevrolet = marcas.First(m => m.Nombre == "Chevrolet");
        modelos.AddRange(new[]
        {
            new Modelo("Cruze", chevrolet.Id),
            new Modelo("Malibu", chevrolet.Id),
            new Modelo("Equinox", chevrolet.Id),
            new Modelo("Silverado", chevrolet.Id)
        });

        // Volkswagen
        var volkswagen = marcas.First(m => m.Nombre == "Volkswagen");
        modelos.AddRange(new[]
        {
            new Modelo("Gol", volkswagen.Id),
            new Modelo("Polo", volkswagen.Id),
            new Modelo("Tiguan", volkswagen.Id),
            new Modelo("Amarok", volkswagen.Id)
        });

        context.Modelo.AddRange(modelos);
        await context.SaveChangesAsync(cancellationToken);
    }
}

