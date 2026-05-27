using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using System.Globalization;

namespace Application.Cars.Search;

internal sealed class SearchCarsQueryHandler : IQueryHandler<SearchCarsQuery, SearchCarsResult>
{
    private readonly IApplicationDbContext _context;

    public SearchCarsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
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
            
        // Mapear a DTOs después de obtener los datos (con seguridad ante nulos)
        var carDtos = cars.Select(c => new CarDto
        {
            Id = c.Id,
            Marca = c.Marca?.Nombre ?? "N/A",
            Modelo = c.Modelo?.Nombre ?? "N/A",
            Anio = c.Anio,
            Precio = c.Price?.Amount ?? 0,
            Descripcion = c.Descripcion ?? string.Empty,
            ImagenPrincipal = GetPrimaryImageUrl(c),
            CantidadPuertas = c.CantidadPuertas,
            Kilometraje = c.Kilometraje
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalResults / (double)pageSize);

        return Result.Success(new SearchCarsResult
        {
            Cars = carDtos,
            TotalResults = totalResults,
            TotalPages = totalPages,
            CurrentPage = page
        });
    }
    
    private string GetPrimaryImageUrl(Car car)
    {
        var primaryImage = car.Images?.FirstOrDefault(i => i.IsPrimary);
        return primaryImage?.ImageUrl;
    }
} 