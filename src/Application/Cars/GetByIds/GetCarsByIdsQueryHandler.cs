using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Cars.Get;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.GetByIds;

internal sealed class GetCarsByIdsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetCarsByIdsQuery, List<CarResponse>>
{
    public async Task<Result<List<CarResponse>>> Handle(GetCarsByIdsQuery query, CancellationToken cancellationToken)
    {
        var cars = await context.Cars
            .Include(c => c.Marca)
            .Include(c => c.Modelo)
            .Include(c => c.Images)
            .Where(c => query.Ids.Contains(c.Id))
            .ToListAsync(cancellationToken);

        var response = cars.Select(car => new CarResponse
        {
            Id = car.Id,
            MarcaId = car.MarcaId,
            Marca = car.Marca.Nombre,
            ModeloId = car.ModeloId,
            Modelo = car.Modelo.Nombre,
            Color = car.Color,
            Type = car.CarType,
            Status = car.CarStatus,
            ServiceStatus = car.ServiceCar,
            Puertas = car.CantidadPuertas,
            Asientos = car.CantidadAsientos,
            Cilindrada = car.Cilindrada,
            Kilometraje = car.Kilometraje,
            Anio = car.Anio,
            Patente = car.Patente.Value,
            Description = car.Descripcion,
            Precio = car.Price.Amount,
            CreatedAt = car.CreatedAt,
            UpdatedAt = car.UpdatedAt,
            Images = car.Images.Select(img => new CarImageResponse
            {
                Id = img.Id,
                ImageUrl = img.ImageUrl,
                IsPrimary = img.IsPrimary,
                Order = img.Order
            }).ToList()
        }).ToList();

        return response;
    }
}
