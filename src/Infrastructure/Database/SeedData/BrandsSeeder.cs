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
/// Seeder para Marcas y Modelos de vehículos.
/// Los IDs son deterministas (hardcoded) para que el cache de Redis
/// no quede inconsistente entre rebuilds del stack de Docker.
/// </summary>
internal static class BrandsSeeder
{
    // -----------------------------------------------------------------------
    // IDs de Marcas — fijos, no regenerar con Guid.NewGuid()
    // -----------------------------------------------------------------------
    public static readonly Guid ToyotaId     = new("10f03083-80f2-4fdc-9c04-43d226c0ad31");
    public static readonly Guid FordId       = new("85df18c3-04b2-4dc1-8c3f-34224f436535");
    public static readonly Guid ChevroletId  = new("a8ab1ca4-9ba2-4774-b604-6f11342da940");
    public static readonly Guid VolkswagenId = new("a24e7857-2701-42b5-88d0-6c6bae8314f5");
    public static readonly Guid HondaId      = new("84092836-c9b3-4cf3-97be-488efe9ace6c");
    public static readonly Guid FiatId       = new("f6b5ad95-2da3-44bd-b18b-b686cb4f95e2");
    public static readonly Guid RenaultId    = new("ebed17d2-dc33-4d2a-ae32-a17cca75536b");
    public static readonly Guid PeugeotId    = new("3a2b2917-6580-4b74-8304-bb6ff4febcf1");

    // -----------------------------------------------------------------------
    // IDs de Modelos — fijos, no regenerar con Guid.NewGuid()
    // -----------------------------------------------------------------------
    // Toyota
    private static readonly Guid CorollaId = new("4a1b2c3d-0001-0001-0001-000000000001");
    private static readonly Guid HiluxId   = new("4a1b2c3d-0001-0001-0001-000000000002");
    private static readonly Guid Rav4Id    = new("4a1b2c3d-0001-0001-0001-000000000003");
    private static readonly Guid YarisId   = new("4a1b2c3d-0001-0001-0001-000000000004");
    // Ford
    private static readonly Guid FiestaId  = new("4a1b2c3d-0002-0002-0002-000000000001");
    private static readonly Guid FocusId   = new("4a1b2c3d-0002-0002-0002-000000000002");
    private static readonly Guid RangerId  = new("4a1b2c3d-0002-0002-0002-000000000003");
    private static readonly Guid MustangId = new("4a1b2c3d-0002-0002-0002-000000000004");
    // Chevrolet
    private static readonly Guid OnixId    = new("4a1b2c3d-0003-0003-0003-000000000001");
    private static readonly Guid CruzeId   = new("4a1b2c3d-0003-0003-0003-000000000002");
    private static readonly Guid TrackerId = new("4a1b2c3d-0003-0003-0003-000000000003");
    private static readonly Guid S10Id     = new("4a1b2c3d-0003-0003-0003-000000000004");
    private static readonly Guid MalibuId  = new("4a1b2c3d-0003-0003-0003-000000000005");
    // Volkswagen
    private static readonly Guid GolId     = new("4a1b2c3d-0004-0004-0004-000000000001");
    private static readonly Guid PoloId    = new("4a1b2c3d-0004-0004-0004-000000000002");
    private static readonly Guid TiguanId  = new("4a1b2c3d-0004-0004-0004-000000000003");
    private static readonly Guid AmarokId  = new("4a1b2c3d-0004-0004-0004-000000000004");

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
            Marca.WithId(ToyotaId,     "Toyota"),
            Marca.WithId(FordId,       "Ford"),
            Marca.WithId(ChevroletId,  "Chevrolet"),
            Marca.WithId(VolkswagenId, "Volkswagen"),
            Marca.WithId(HondaId,      "Honda"),
            Marca.WithId(FiatId,       "Fiat"),
            Marca.WithId(RenaultId,    "Renault"),
            Marca.WithId(PeugeotId,    "Peugeot"),
        };

        context.Marca.AddRange(marcas);
        await context.SaveChangesAsync(cancellationToken);

        var modelos = new List<Modelo>
        {
            // Toyota
            Modelo.WithId(CorollaId, "Corolla", ToyotaId),
            Modelo.WithId(HiluxId,   "Hilux",   ToyotaId),
            Modelo.WithId(Rav4Id,    "RAV4",    ToyotaId),
            Modelo.WithId(YarisId,   "Yaris",   ToyotaId),
            // Ford
            Modelo.WithId(FiestaId,  "Fiesta",  FordId),
            Modelo.WithId(FocusId,   "Focus",   FordId),
            Modelo.WithId(RangerId,  "Ranger",  FordId),
            Modelo.WithId(MustangId, "Mustang", FordId),
            // Chevrolet
            Modelo.WithId(OnixId,    "Onix",    ChevroletId),
            Modelo.WithId(CruzeId,   "Cruze",   ChevroletId),
            Modelo.WithId(TrackerId, "Tracker", ChevroletId),
            Modelo.WithId(S10Id,     "S10",     ChevroletId),
            Modelo.WithId(MalibuId,  "Malibu",  ChevroletId),
            // Volkswagen
            Modelo.WithId(GolId,     "Gol",     VolkswagenId),
            Modelo.WithId(PoloId,    "Polo",    VolkswagenId),
            Modelo.WithId(TiguanId,  "Tiguan",  VolkswagenId),
            Modelo.WithId(AmarokId,  "Amarok",  VolkswagenId),
        };

        context.Modelo.AddRange(modelos);
        await context.SaveChangesAsync(cancellationToken);
    }
}
