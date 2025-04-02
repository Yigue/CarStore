using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.GetAll;

internal sealed class GetAllCarsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetAllCarsQuery, List<CarsResponses>>
{
    public async Task<Result<List<CarsResponses>>> Handle(GetAllCarsQuery query, CancellationToken cancellationToken)
    {
        // Primero, cargamos los autos con sus imágenes
        var cars = await context.Cars
            .Include(c => c.Marca)
            .Include(c => c.Modelo)
            .Include(c => c.Images)
            .ToListAsync(cancellationToken);

        // Luego, construimos las respuestas en memoria (esto evita el error de traducción LINQ)
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
            car.Año,
            car.Patente,
            car.Descripcion,
            car.Price,
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

        return responses;
    }
}
    
