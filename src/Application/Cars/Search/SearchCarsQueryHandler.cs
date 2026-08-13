using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Cars;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;
using System.Globalization;

namespace Application.Cars.Search;

internal sealed class SearchCarsQueryHandler : IQueryHandler<SearchCarsQuery, SearchCarsResult>
{
    private static readonly TimeSpan ReadTtl = TimeSpan.FromMinutes(15);

    private readonly IApplicationDbContext _context;
    private readonly IStorageService _storage;
    private readonly ILogger<SearchCarsQueryHandler> _logger;

    public SearchCarsQueryHandler(
        IApplicationDbContext context,
        IStorageService storage,
        ILogger<SearchCarsQueryHandler> logger)
    {
        _context = context;
        _storage = storage;
        _logger = logger;
    }

    public async Task<Result<SearchCarsResult>> Handle(SearchCarsQuery query, CancellationToken cancellationToken)
    {
        var carsQuery = _context.Cars
            .Include(c => c.Marca)
            .Include(c => c.Modelo)
            .Include(c => c.Images)
            .AsQueryable();

        // Aplicar filtros
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm;
            carsQuery = carsQuery.Where(c =>
                c.Marca.Nombre.Contains(term) ||
                c.Modelo.Nombre.Contains(term) ||
                (c.Descripcion != null && c.Descripcion.Contains(term)));
        }

        if (query.MarcaId.HasValue && query.MarcaId.Value != Guid.Empty)
        {
            carsQuery = carsQuery.Where(c => c.MarcaId == query.MarcaId);
        }

        if (query.ModeloId.HasValue && query.ModeloId.Value != Guid.Empty)
        {
            carsQuery = carsQuery.Where(c => c.ModeloId == query.ModeloId);
        }

        // Importante: usar .Value (no la propiedad nullable) para evitar que EF Core
        // genere un cast a (int?)/(decimal?) que rompe la traducción a SQL con value objects.
        if (query.YearFrom.HasValue)
        {
            var yearFrom = query.YearFrom.Value;
            carsQuery = carsQuery.Where(c => c.Anio >= yearFrom);
        }

        if (query.YearTo.HasValue)
        {
            var yearTo = query.YearTo.Value;
            carsQuery = carsQuery.Where(c => c.Anio <= yearTo);
        }

        // NOTA: el filtro/orden por precio se aplica en memoria porque Money está mapeado con un
        // ValueConverter Money<->decimal sobre una columna única, lo que impide a EF Core traducir
        // ni `c.Price.Amount` ni `EF.Property<decimal>(c, "Price")` (el converter falla al armar
        // el parámetro). Para datasets de concesionaria es asumible — si crece, mover Price a
        // entidad owned o columna decimal explícita.

        if (query.ColorIds != null && query.ColorIds.Any())
        {
            var colorValues = query.ColorIds.Select(id => (Color)id).ToList();
            carsQuery = carsQuery.Where(c => colorValues.Contains(c.Color));
        }

        if (query.CarTypeIds != null && query.CarTypeIds.Any())
        {
            var carTypeValues = query.CarTypeIds.Select(id => (TypeCar)id).ToList();
            carsQuery = carsQuery.Where(c => carTypeValues.Contains(c.CarType));
        }

        if (query.DoorsFrom.HasValue)
        {
            var doorsFrom = query.DoorsFrom.Value;
            carsQuery = carsQuery.Where(c => c.CantidadPuertas >= doorsFrom);
        }

        if (query.DoorsTo.HasValue)
        {
            var doorsTo = query.DoorsTo.Value;
            carsQuery = carsQuery.Where(c => c.CantidadPuertas <= doorsTo);
        }

        // Ordenamiento server-side (excepto precio, que se ordena en memoria más abajo)
        var sortKey = query.SortBy?.ToUpperInvariant();
        carsQuery = sortKey switch
        {
            "YEAR" => query.SortDescending
                ? carsQuery.OrderByDescending(c => c.Anio)
                : carsQuery.OrderBy(c => c.Anio),
            "CREATED" => query.SortDescending
                ? carsQuery.OrderByDescending(c => c.CreatedAt)
                : carsQuery.OrderBy(c => c.CreatedAt),
            "PRICE" => carsQuery, // ordenamos después en memoria
            _ => query.SortDescending
                ? carsQuery.OrderByDescending(c => c.CreatedAt)
                : carsQuery.OrderBy(c => c.CreatedAt)
        };

        // Materializamos para poder aplicar filtros/ordenamiento de Money en memoria
        var allFiltered = await carsQuery.ToListAsync(cancellationToken);

        if (query.PriceFrom.HasValue)
        {
            var priceFrom = query.PriceFrom.Value;
            allFiltered = allFiltered.Where(c => c.Price.Amount >= priceFrom).ToList();
        }

        if (query.PriceTo.HasValue)
        {
            var priceTo = query.PriceTo.Value;
            allFiltered = allFiltered.Where(c => c.Price.Amount <= priceTo).ToList();
        }

        if (sortKey == "PRICE")
        {
            allFiltered = query.SortDescending
                ? allFiltered.OrderByDescending(c => c.Price.Amount).ToList()
                : allFiltered.OrderBy(c => c.Price.Amount).ToList();
        }

        var totalResults = allFiltered.Count;

        // Paginación en memoria sobre el conjunto ya filtrado
        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize > 0 ? query.PageSize : 10;

        var cars = allFiltered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Mapear a DTOs después de obtener los datos. La lectura del primary image es
        // async (puede presignar) — procesamos secuencialmente porque la lista está
        // acotada por pageSize.
        var carDtos = new List<CarDto>(cars.Count);
        foreach (var c in cars)
        {
            carDtos.Add(new CarDto
            {
                Id = c.Id,
                Marca = c.Marca?.Nombre ?? "N/A",
                Modelo = c.Modelo?.Nombre ?? "N/A",
                Anio = c.Anio,
                Precio = c.Price?.Amount ?? 0,
                Descripcion = c.Descripcion ?? string.Empty,
                ImagenPrincipal = await GetPrimaryImageUrl(c, cancellationToken),
                CantidadPuertas = c.CantidadPuertas,
                Kilometraje = c.Kilometraje,
                CantidadAsientos = c.CantidadAsientos,
                Cilindrada = c.Cilindrada,
                Color = c.Color,
                CarType = c.CarType,
                Status = c.CarStatus,
                ServiceStatus = c.ServiceCar,
                FuelType = c.FuelType,
                Transmission = c.Transmission,
                Featured = c.Featured,
                Patente = c.Patente?.Value ?? string.Empty
            });
        }

        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);

        return Result.Success(new SearchCarsResult
        {
            Cars = carDtos,
            TotalResults = totalResults,
            TotalPages = totalPages,
            CurrentPage = page
        });
    }

    /// <summary>
    /// Resolves the primary image URL for a car. Defensive read-path per REQ-FVIP-2:
    /// prefers a cover image with a presignable <c>object_key</c>; falls back to the
    /// legacy direct <c>image_url</c> when no MinIO object exists; returns the stable
    /// placeholder path with a WARN log when every URL field is null/empty.
    /// </summary>
    internal async Task<string> GetPrimaryImageUrl(Car car, CancellationToken cancellationToken)
    {
        if (car.Images is null || car.Images.Count == 0)
        {
            return LogPlaceholder(car.Id, imageId: null);
        }

        // 1. Prefer a cover image.
        CarImage? cover = car.Images.FirstOrDefault(i => i.IsCover);

        if (cover is not null)
        {
            return await ResolveImageUrl(cover, car.Id, cancellationToken);
        }

        // 2. No cover at all — try any image with a usable URL, prefer modern.
        CarImage? modernFallback = car.Images.FirstOrDefault(i => !string.IsNullOrEmpty(i.ObjectKey));
        if (modernFallback is not null)
        {
            return await ResolveImageUrl(modernFallback, car.Id, cancellationToken);
        }

        CarImage? legacyFallback = car.Images.FirstOrDefault(i => !string.IsNullOrEmpty(i.ImageUrl));
        if (legacyFallback is not null)
        {
            // Legacy dual-mode (ADR-2): return the direct URL as-is. No presign — the
            // URL is already a public CDN/Azure blob URL, not a MinIO object key.
            return legacyFallback.ImageUrl!;
        }

        return LogPlaceholder(car.Id, imageId: null);
    }

    private async Task<string> ResolveImageUrl(CarImage image, Guid carId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(image.ObjectKey))
        {
            // Modern path: presign a fresh URL.
            try
            {
                Uri presigned = await _storage.GetPresignedUrlAsync(image.ObjectKey, ReadTtl, cancellationToken);
                return presigned.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "SearchCars: presign failed for car {CarId} image {ImageId} (object_key={ObjectKey}); falling back to placeholder.",
                    carId, image.Id, image.ObjectKey);
                return CarImageDefaults.NoImagePlaceholderUrl;
            }
        }

        if (!string.IsNullOrEmpty(image.ImageUrl))
        {
            return image.ImageUrl!;
        }

        return LogPlaceholder(carId, image.Id);
    }

    private string LogPlaceholder(Guid carId, Guid? imageId)
    {
        _logger.LogWarning(
            "SearchCars: car {CarId} has no usable image (imageId={ImageId}); returning stable placeholder {Placeholder}.",
            carId, imageId, CarImageDefaults.NoImagePlaceholderUrl);
        return CarImageDefaults.NoImagePlaceholderUrl;
    }
}
