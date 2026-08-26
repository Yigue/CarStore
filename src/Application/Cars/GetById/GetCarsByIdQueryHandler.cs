using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Cars.GetAll;
using Application.Cars.GetById;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.GetById;

internal sealed class GetCarByIdQueryHandler(IApplicationDbContext context, IStorageService storage, IUserContext userContext)
    : IQueryHandler<GetCarByIdQuery, CarGetByIdResponse>
{
    public async Task<Result<CarGetByIdResponse>> Handle(GetCarByIdQuery query, CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .Include(c => c.Marca)
            .Include(c => c.Modelo)
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == query.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure<CarGetByIdResponse>(CarErrors.NotFound(query.CarId));
        }

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
            imageResponses.Add(new CarImageResponse(img.Id, url, img.IsCover, img.DisplayOrder));
        }

        var response = new CarGetByIdResponse
        (
            car.Id,
            car.Marca.Nombre,
            car.Modelo.Nombre,
            car.Color,
            car.CarType,
            car.CarStatus,
            car.ServiceCar,
            car.FuelType,
            car.Transmission,
            car.CantidadPuertas,
            car.CantidadAsientos,
            car.Cilindrada,
            car.Kilometraje,
            car.Anio,
            // La patente identifica un vehículo físico concreto y este endpoint
            // es AllowAnonymous, así que viajaba a cualquiera que pidiera el id
            // — y el catálogo público la metía además en el título de compartir.
            // Se corta en "logueado", no en "admin": un vendedor la necesita.
            userContext.IsAuthenticated ? car.Patente.Value : null,
            car.Descripcion,
            car.Price.Amount,
            car.CreatedAt,
            car.UpdatedAt,
            imageResponses,
            car.Featured,
            // Este endpoint es AllowAnonymous porque lo usa la ficha del catálogo
            // público. PurchaseCost es el costo de compra: el margen de la
            // concesionaria, no un dato del vehículo. Sólo un admin lo ve; para
            // cualquier otro — incluido el visitante anónimo — viaja como null.
            userContext.IsAdmin ? car.PurchaseCost?.Amount : null
        );

        return response;
    }
}
