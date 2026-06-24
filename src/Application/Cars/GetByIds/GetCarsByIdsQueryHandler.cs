using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Cars.Get;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.GetByIds;

internal sealed class GetCarsByIdsQueryHandler(IApplicationDbContext context, IStorageService storage)
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

        var response = new List<CarResponse>(cars.Count);
        foreach (var car in cars)
        {
            var imageResponses = new List<CarImageResponse>(car.Images.Count);
            foreach (var img in car.Images.OrderBy(i => i.DisplayOrder))
            {
                string url = img.ImageUrl ?? string.Empty;
                if (img.ObjectKey is not null)
                {
                    try
                    {
                        Uri presigned = await storage.GetPresignedUrlAsync(img.ObjectKey, TimeSpan.FromMinutes(15), cancellationToken);
                        url = presigned.ToString();
                    }
                    catch
                    {
                        // Fallback
                    }
                }
                imageResponses.Add(new CarImageResponse
                {
                    Id = img.Id,
                    ImageUrl = url,
                    IsPrimary = img.IsCover,
                    Order = img.DisplayOrder
                });
            }

            response.Add(new CarResponse
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
                Images = imageResponses
            });
        }

        return response;
    }
}
