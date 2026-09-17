using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Shared.ValueObjects;
using Application.Abstractions.Caching;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Update;

internal sealed class UpdateCarCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICachedBrandService cachedBrandService,
    ICachedModelService cachedModelService)
    : ICommandHandler<UpdateCarCommand, Guid>
{
    public async Task<Result<Guid>> Handle(UpdateCarCommand command, CancellationToken cancellationToken)
    {
        // Include the gallery: the cover is part of what an update can change (INV-01), and
        // reassigning it has to see every sibling to demote the previous one.
        Car? car = await context.Cars
            .Include(c => c.Images)
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (car is null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.Id));
        }
        
        // Validate existence via the cache (fast path)...
        if (await cachedBrandService.GetByIdAsync(command.Marca, cancellationToken) is null)
        {
            return Result.Failure<Guid>(CarErrors.AtributesInvalid());
        }

        if (await cachedModelService.GetByIdAsync(command.Modelo, cancellationToken) is null)
        {
            return Result.Failure<Guid>(CarErrors.AtributesInvalid());
        }

        // ...but attach the instances tracked by THIS context. The cache round-trips entities
        // through Redis, so on a hit it returns detached graphs that each carry their own Marca
        // instance; attaching those duplicates to the tracked car makes EF throw an identity
        // conflict ("another instance with the same key is already being tracked") on SaveChanges.
        // Loading from the context (no Include on the model) guarantees a single tracked Marca.
        Marca marca = await context.Marca
            .SingleAsync(m => m.Id == command.Marca, cancellationToken);
        Modelo modelo = await context.Modelo
            .SingleAsync(m => m.Id == command.Modelo, cancellationToken);

        // Update properties that need to be public for EF Core
        // Update properties that need to be public for EF Core
        car.UpdateDetails(
            marca,
            modelo,
            command.Color,
            command.CarType,
            command.CarStatus,
            command.ServiceCar,
            command.CantidadPuertas,
            command.CantidadAsientos,
            command.Cilindrada,
            command.Kilometraje,
            command.Anio,
            command.Patente,
            command.Descripcion,
            dateTimeProvider.UtcNow,
            command.FuelType,
            command.Featured,
            command.Transmission,
            command.PurchaseCost);
        
        // Use domain method for price update
        if (car.Price.Amount != command.Price)
        {
            car.UpdatePrice(command.Price, dateTimeProvider.UtcNow);
        }
        
        // Handle service status changes using domain methods
        if (command.ServiceCar == StatusServiceCar.Vendido && car.ServiceCar != StatusServiceCar.Vendido)
        {
            car.MarkAsSold(dateTimeProvider.UtcNow);
        }
        else if (command.ServiceCar == StatusServiceCar.Disponible && car.ServiceCar != StatusServiceCar.Disponible)
        {
            car.MarkAsAvailable(dateTimeProvider.UtcNow);
        }

        // INV-01: the cover travels with the rest of the vehicle's data.
        //
        // Until now the only way to reassign it was PATCH cars/{id}/images/{imageId}/cover, a
        // separate call the edit form never made — so "editar el vehículo" could change fifteen
        // fields and not the one the buyer sees first. Null leaves the gallery untouched, which
        // is what every pre-existing caller sends.
        if (command.CoverImageId is { } coverImageId)
        {
            Result coverResult = ApplyCover(car, coverImageId);
            if (coverResult.IsFailure)
            {
                return Result.Failure<Guid>(coverResult.Error);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(car.Id);
    }

    /// <summary>
    /// Promotes one image to cover and demotes the rest. Demote-before-promote, like
    /// <c>SetCoverImageCommandHandler</c>: the partial unique index allows a single cover per
    /// car, so a moment with two of them fails the constraint mid-transaction.
    /// </summary>
    private static Result ApplyCover(Car car, Guid coverImageId)
    {
        CarImage? target = car.Images.FirstOrDefault(i => i.Id == coverImageId);

        if (target is null)
        {
            return Result.Failure(CarErrors.ImageNotFoundInCar(coverImageId, car.Id));
        }

        foreach (CarImage image in car.Images.Where(i => i.IsCover && i.Id != target.Id))
        {
            image.SetAsCover(false);
        }

        target.SetAsCover(true);

        return Result.Success();
    }
}
