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
        List<CarsResponses> cars = await context.Cars
            .Select(car => new CarsResponses(
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
                car.UpdatedAt
            ))

            .ToListAsync(cancellationToken);

        return cars;
    }
}
    
