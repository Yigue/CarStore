using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Cars.GetById;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.GetById;

internal sealed class GetCarByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetCarByIdQuery, CarGetByIdResponse>
{
    public async Task<Result<CarGetByIdResponse>> Handle(GetCarByIdQuery query, CancellationToken cancellationToken)
    {
        CarGetByIdResponse? car = await context.Cars
            .Where(car => car.Id == query.CarId)
            .Select(car => new CarGetByIdResponse
            (
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
                car.Patente.Value,
                car.Descripcion,
                car.Price.Amount,
                car.CreatedAt,
                car.UpdatedAt
            ))

            .SingleOrDefaultAsync(cancellationToken);

        if (car is null)
        {
            return Result.Failure<CarGetByIdResponse>(CarErrors.NotFound(query.CarId));
        }

        return car;
    }
}
