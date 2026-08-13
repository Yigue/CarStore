using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.GetAll;

internal sealed class GetAllCarsQueryHandler(IApplicationDbContext context, IStorageService storage, IUserContext userContext)
    : IQueryHandler<GetAllCarsQuery, PaginatedResult<CarsResponses>>
{
    public async Task<Result<PaginatedResult<CarsResponses>>> Handle(GetAllCarsQuery query, CancellationToken cancellationToken)
    {
        var carsQuery = context.Cars
            .AsNoTracking()
            .Include(c => c.Marca)
            .Include(c => c.Modelo)
            .Include(c => c.Images);

        Result<IOrderedQueryable<Car>> ordering = CarSortOrder.Apply(carsQuery, query.SortBy, query.SortOrder);

        if (ordering.IsFailure)
        {
            return Result.Failure<PaginatedResult<CarsResponses>>(ordering.Error);
        }

        var totalCount = await carsQuery.CountAsync(cancellationToken);

        // Sort is applied to the whole set before Skip/Take, so page N holds the Nth
        // slice of the sorted inventory rather than a re-ordered arbitrary page.
        var cars = await ordering.Value
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
                car.FuelType,
                car.Transmission,
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
                // El costo de compra es el margen de la concesionaria. El
                // endpoint ya exige cars:read, pero eso lo tiene también un
                // vendedor: sólo el admin ve el costo. La misma regla que
                // VehicleForm aplica al guardar, ahora también al leer.
                userContext.IsAdmin ? car.PurchaseCost?.Amount : null
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
