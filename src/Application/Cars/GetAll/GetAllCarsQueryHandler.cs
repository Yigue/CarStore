using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.GetAll;

internal sealed class GetAllCarsQueryHandler(IApplicationDbContext context, IStorageService storage)
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

        var responses = new List<CarsResponses>(cars.Count);
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
                        // Fallback to ImageUrl if presign fails
                    }
                }
                imageResponses.Add(new CarImageResponse(img.Id, url, img.IsCover, img.DisplayOrder));
            }

            responses.Add(new CarsResponses(
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
                imageResponses,
                car.Featured,
                car.PurchaseCost?.Amount
            ));
        }

        var paginatedResult = new PaginatedResult<CarsResponses>(
            responses,
            totalCount,
            query.Page,
            query.PageSize);

        return Result.Success(paginatedResult);
    }
}
