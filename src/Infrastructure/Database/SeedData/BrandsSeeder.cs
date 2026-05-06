using Application.Abstractions.Data;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Cars;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// Seeder para Marcas y Modelos de vehÃ­culos.
/// </summary>
internal static class BrandsSeeder
{
    public static async Task SeedAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken = default)
    {
        // Chequeo sincronico e idempotente para evitar problemas en tests
        if (context.Marca.IgnoreQueryFilters().Any())
        {
            return;
        }

        var marcas = new List<Marca>
        {
            new Marca("Toyota"),
            new Marca("Ford"),
            new Marca("Chevrolet"),
            new Marca("Volkswagen"),
            new Marca("Honda"),
            new Marca("Fiat"),
            new Marca("Renault"),
            new Marca("Peugeot")
        };

        context.Marca.AddRange(marcas);
        await context.SaveChangesAsync(cancellationToken);

        var toyota = marcas.First(m => m.Nombre == "Toyota");
        var ford = marcas.First(m => m.Nombre == "Ford");
        var chevrolet = marcas.First(m => m.Nombre == "Chevrolet");
        var volkswagen = marcas.First(m => m.Nombre == "Volkswagen");

        var modelos = new List<Modelo>();
        modelos.AddRange(new[] {
            new Modelo("Corolla", toyota.Id),
            new Modelo("Hilux", toyota.Id),
            new Modelo("RAV4", toyota.Id),
            new Modelo("Yaris", toyota.Id)
        });

        modelos.AddRange(new[] {
            new Modelo("Fiesta", ford.Id),
            new Modelo("Focus", ford.Id),
            new Modelo("Ranger", ford.Id),
            new Modelo("Mustang", ford.Id)
        });

        modelos.AddRange(new[] {
            new Modelo("Onix", chevrolet.Id),
            new Modelo("Cruze", chevrolet.Id),
            new Modelo("Tracker", chevrolet.Id),
            new Modelo("S10", chevrolet.Id),
            new Modelo("Malibu", chevrolet.Id)
        });

        modelos.AddRange(new[] {
            new Modelo("Gol", volkswagen.Id),
            new Modelo("Polo", volkswagen.Id),
            new Modelo("Tiguan", volkswagen.Id),
            new Modelo("Amarok", volkswagen.Id)
        });

        context.Modelo.AddRange(modelos);
        await context.SaveChangesAsync(cancellationToken);
    }
}
