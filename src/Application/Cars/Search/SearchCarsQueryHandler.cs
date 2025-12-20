using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Atribbutes;
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
            .Include(c => c.Images.Where(i => i.IsPrimary))
            .AsQueryable();

        // Aplicar filtros
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            carsQuery = carsQuery.Where(c => 
                c.Marca.Nombre.Contains(query.SearchTerm) || 
                c.Modelo.Nombre.Contains(query.SearchTerm) ||
                c.Descripcion.Contains(query.SearchTerm));
        }

        if (query.MarcaId.HasValue)
        {
            carsQuery = carsQuery.Where(c => c.MarcaId == query.MarcaId);
        }

        if (query.ModeloId.HasValue)
        {
            carsQuery = carsQuery.Where(c => c.ModeloId == query.ModeloId);
        }

        if (query.YearFrom.HasValue)
        {
            carsQuery = carsQuery.Where(c => c.Año >= query.YearFrom);
        }

        if (query.YearTo.HasValue)
        {
            carsQuery = carsQuery.Where(c => c.Año <= query.YearTo);
        }

        if (query.PriceFrom.HasValue)
        {
            carsQuery = carsQuery.Where(c => c.Price.Amount >= query.PriceFrom);
        }

        if (query.PriceTo.HasValue)
        {
            carsQuery = carsQuery.Where(c => c.Price.Amount <= query.PriceTo);
        }

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
            carsQuery = carsQuery.Where(c => c.CantidadPuertas >= query.DoorsFrom);
        }

        if (query.DoorsTo.HasValue)
        {
            carsQuery = carsQuery.Where(c => c.CantidadPuertas <= query.DoorsTo);
        }

        // Total de resultados antes de paginación
        var totalResults = await carsQuery.CountAsync(cancellationToken);

        // Ordenamiento
        carsQuery = query.SortBy?.ToUpperInvariant() switch
        {
            "PRICE" => query.SortDescending 
                ? carsQuery.OrderByDescending(c => c.Price.Amount)
                : carsQuery.OrderBy(c => c.Price.Amount),
            "YEAR" => query.SortDescending 
                ? carsQuery.OrderByDescending(c => c.Año)
                : carsQuery.OrderBy(c => c.Año),
            "CREATED" => query.SortDescending 
                ? carsQuery.OrderByDescending(c => c.CreatedAt)
                : carsQuery.OrderBy(c => c.CreatedAt),
            _ => carsQuery.OrderByDescending(c => c.CreatedAt)
        };

        // Paginación
        var cars = await carsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
            
        // Mapear a DTOs después de obtener los datos
        var carDtos = cars.Select(c => new CarDto
        {
            Id = c.Id,
            Marca = c.Marca.Nombre,
            Modelo = c.Modelo.Nombre,
            Año = c.Año,
            Precio = c.Price.Amount,
            Descripcion = c.Descripcion,
            ImagenPrincipal = GetPrimaryImageUrl(c),
            CantidadPuertas = c.CantidadPuertas,
            Kilometraje = c.Kilometraje
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalResults / (double)query.PageSize);

        return Result.Success(new SearchCarsResult
        {
            Cars = carDtos,
            TotalResults = totalResults,
            TotalPages = totalPages,
            CurrentPage = query.Page
        });
    }
    
    private string GetPrimaryImageUrl(Car car)
    {
        var primaryImage = car.Images?.FirstOrDefault(i => i.IsPrimary);
        return primaryImage?.ImageUrl;
    }
} 