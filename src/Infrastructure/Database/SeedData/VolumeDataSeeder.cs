using Application.Abstractions.Data;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Database.SeedData;

/// <summary>
/// EXT-01: seeder de volumen para probar la paginación del frontend con miles de
/// registros. Deliberadamente independiente de <see cref="DatabaseSeeder"/> / <see cref="DevDataSeeder"/>
/// (que sólo agregan un puñado de filas de demo): no corre en cada arranque de dev/testing, sólo
/// cuando se lo habilita explícitamente con <c>Seeding:VolumeData:Enabled</c> (variable de entorno
/// <c>Seeding__VolumeData__Enabled=true</c>), porque generar miles de filas en cada `dotnet run`
/// o en cada corrida de la suite de integración sería costoso y no aporta nada al desarrollo normal.
///
/// Siembra bajo el mismo dealer que <see cref="DevDataSeeder"/> (<see cref="DefaultDealerId"/>) para
/// que las credenciales de admin ya sembradas por <c>UsersSeeder</c> sirvan para loguearse y navegar
/// la data de inmediato. Es idempotente: si ya hay al menos la cantidad configurada de autos/leads
/// para ese dealer, no vuelve a insertar.
/// </summary>
internal static class VolumeDataSeeder
{
    // Mismo Guid que DevDataSeeder.DefaultDealerId — se duplica el literal (en vez de compartir un
    // campo) porque ambos seeders son independientes a propósito; lo que importa es que apunten al
    // mismo dealer de desarrollo.
    private static readonly Guid DefaultDealerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const int DefaultCarCount = 3000;
    private const int DefaultLeadCount = 5000;
    private const int BatchSize = 500;

    private static readonly string[] FirstNames =
    [
        "Juan", "Maria", "Carlos", "Ana", "Luis", "Laura", "Pedro", "Sofia", "Diego", "Valentina",
        "Martin", "Camila", "Jorge", "Lucia", "Fernando", "Julieta", "Ricardo", "Paula", "Sergio", "Daniela",
        "Pablo", "Florencia", "Matias", "Agustina", "Nicolas",
    ];

    private static readonly string[] LastNames =
    [
        "Gonzalez", "Rodriguez", "Perez", "Fernandez", "Lopez", "Martinez", "Garcia", "Sanchez", "Romero", "Diaz",
        "Alvarez", "Torres", "Ruiz", "Ramirez", "Flores", "Acosta", "Benitez", "Medina", "Herrera", "Suarez",
        "Rojas", "Ortiz", "Silva", "Castro", "Nunez",
    ];

    public static async Task SeedAsync(
        IApplicationDbContext context,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!bool.TryParse(configuration["Seeding:VolumeData:Enabled"], out bool enabled) || !enabled)
        {
            return;
        }

        int carCount = int.TryParse(configuration["Seeding:VolumeData:CarCount"], out int configuredCars)
            ? configuredCars
            : DefaultCarCount;
        int leadCount = int.TryParse(configuration["Seeding:VolumeData:LeadCount"], out int configuredLeads)
            ? configuredLeads
            : DefaultLeadCount;

        logger.LogInformation(
            "VolumeDataSeeder habilitado: objetivo {CarCount} autos y {LeadCount} leads para el dealer {DealerId}",
            carCount, leadCount, DefaultDealerId);

        List<Car> cars = await SeedCarsAsync(context, carCount, logger, cancellationToken);
        await SeedLeadsAsync(context, cars, leadCount, logger, cancellationToken);
    }

    private static async Task<List<Car>> SeedCarsAsync(
        IApplicationDbContext context,
        int carCount,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        int existingCount = await context.Cars.IgnoreQueryFilters()
            .CountAsync(c => c.DealerId == DefaultDealerId, cancellationToken);

        if (existingCount >= carCount)
        {
            logger.LogInformation(
                "VolumeDataSeeder: ya hay {ExistingCount} autos (>= {CarCount}), no se generan más.",
                existingCount, carCount);
            return await context.Cars.IgnoreQueryFilters()
                .Where(c => c.DealerId == DefaultDealerId)
                .ToListAsync(cancellationToken);
        }

        List<Marca> marcas = await context.Marca.IgnoreQueryFilters().ToListAsync(cancellationToken);
        List<Modelo> modelos = await context.Modelo.IgnoreQueryFilters().ToListAsync(cancellationToken);
        if (marcas.Count == 0 || modelos.Count == 0)
        {
            logger.LogWarning("VolumeDataSeeder: no hay marcas/modelos cargados todavía, se omite la generación de autos.");
            return [];
        }

        var random = new Random(12345);
        var colors = Enum.GetValues<Color>();
        var carTypes = Enum.GetValues<TypeCar>();
        var carStatuses = Enum.GetValues<StatusCar>();
        var fuelTypes = Enum.GetValues<FuelType>();
        var transmissions = Enum.GetValues<Transmission>();

        // Sesgada hacia "Disponible" — es lo que se ve mayormente en el catálogo público
        // y en el listado principal de inventario, que es lo que se quiere paginar.
        var serviceStatuses = new[]
        {
            StatusServiceCar.Disponible, StatusServiceCar.Disponible, StatusServiceCar.Disponible,
            StatusServiceCar.EnVenta, StatusServiceCar.Vendido, StatusServiceCar.Reservado,
            StatusServiceCar.NoDisponible, StatusServiceCar.Service,
        };

        int toCreate = carCount - existingCount;
        var created = new List<Car>(toCreate);
        var batch = new List<Car>(BatchSize);
        DateTime now = DateTime.UtcNow;

        for (int i = existingCount + 1; i <= existingCount + toCreate; i++)
        {
            Marca marca = marcas[random.Next(marcas.Count)];
            List<Modelo> modelosDeMarca = modelos.Where(m => m.MarcaId == marca.Id).ToList();
            Modelo modelo = modelosDeMarca.Count > 0
                ? modelosDeMarca[random.Next(modelosDeMarca.Count)]
                : modelos[random.Next(modelos.Count)];
            int anio = random.Next(2010, 2025);

            var car = new Car(
                DefaultDealerId,
                marca,
                modelo,
                colors[random.Next(colors.Length)],
                carTypes[random.Next(carTypes.Length)],
                carStatuses[random.Next(carStatuses.Length)],
                serviceStatuses[random.Next(serviceStatuses.Length)],
                cantidadPuertas: random.Next(2, 6),
                cantidadAsientos: random.Next(2, 8),
                cilindrada: random.Next(1000, 3200),
                kilometraje: random.Next(0, 150_000),
                anio: anio,
                patente: $"VOL{i:D4}",
                descripcion: $"{marca.Nombre} {modelo.Nombre} {anio}",
                price: random.Next(8000, 90_000),
                date: now.AddDays(-random.Next(0, 365)),
                fuelType: fuelTypes[random.Next(fuelTypes.Length)],
                transmission: transmissions[random.Next(transmissions.Length)]);

            batch.Add(car);
            created.Add(car);

            if (batch.Count >= BatchSize)
            {
                context.Cars.AddRange(batch);
                await context.SaveChangesAsync(cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            context.Cars.AddRange(batch);
            await context.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("VolumeDataSeeder: {Count} autos generados.", created.Count);
        return created;
    }

    private static async Task SeedLeadsAsync(
        IApplicationDbContext context,
        List<Car> cars,
        int leadCount,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        int existingCount = await context.Leads.IgnoreQueryFilters()
            .CountAsync(l => l.DealerId == DefaultDealerId, cancellationToken);

        if (existingCount >= leadCount)
        {
            logger.LogInformation(
                "VolumeDataSeeder: ya hay {ExistingCount} leads (>= {LeadCount}), no se generan más.",
                existingCount, leadCount);
            return;
        }

        var random = new Random(54321);
        var sources = Enum.GetValues<LeadSource>();

        int toCreate = leadCount - existingCount;
        var batch = new List<Lead>(BatchSize);
        DateTime now = DateTime.UtcNow;
        int createdCount = 0;

        for (int i = existingCount + 1; i <= existingCount + toCreate; i++)
        {
            string firstName = FirstNames[random.Next(FirstNames.Length)];
            string lastName = LastNames[random.Next(LastNames.Length)];
            string email = $"volumen.lead{i}@carstore-dev.test";
            string phone = $"+54 11 {random.Next(1000, 9999)}-{random.Next(1000, 9999)}";
            Guid? interestedCarId = cars.Count > 0 && random.Next(0, 2) == 0
                ? cars[random.Next(cars.Count)].Id
                : null;

            Lead lead = Lead.Create(
                DefaultDealerId,
                $"{firstName} {lastName}",
                email,
                phone,
                sources[random.Next(sources.Length)],
                now.AddDays(-random.Next(0, 180)),
                interestedCarId);

            batch.Add(lead);
            createdCount++;

            if (batch.Count >= BatchSize)
            {
                context.Leads.AddRange(batch);
                await context.SaveChangesAsync(cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            context.Leads.AddRange(batch);
            await context.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("VolumeDataSeeder: {Count} leads generados.", createdCount);
    }
}
