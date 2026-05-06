using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.GetAll;

internal sealed class GetAllCarsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllCarsQuery, PaginatedResult<CarsResponses>>
{
    public async Task<Result<PaginatedResult<CarsResponses>>> Handle(GetAllCarsQuery query, CancellationToken cancellationToken)
    {
        var carsQuery = context.Cars
            .AsNoTracking()
            .Include(c => c.Marca)
            .Include(c => c.Modelo)
            .Include(c => c.Images);

        var totalCount = await carsQuery.CountAsync(cancellationToken);

        var cars = await carsQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        // Construimos las respuestas en memoria (esto evita el error de traducción LINQ)
        var responses = cars.Select(car => new CarsResponses(
            car.Id,
            car.Marca.Nombre,
            car.Modelo.Nombre,
            car.Color,
            car.CarType,
            car.CarStatus,
            car.ServiceCar,
            car.CantidadPuertas,
            car.CantidadAsientos,
            car.Cilindrada,
            car.Kilometraje,
            car.Anio,
            car.Patente.Value,
            car.Descripcion,
            car.Price.Amount,
            car.CreatedAt,
            car.UpdatedAt,
            car.Images
                .OrderBy(img => img.Order)
                .Select(img => new CarImageResponse(
                    img.Id,
                    img.ImageUrl,
                    img.IsPrimary,
                    img.Order
                ))
                .ToList()
        )).ToList();

        var paginatedResult = new PaginatedResult<CarsResponses>(
            responses,
            totalCount,
            query.Page,
            query.PageSize);

        return Result.Success(paginatedResult);
    }
}
