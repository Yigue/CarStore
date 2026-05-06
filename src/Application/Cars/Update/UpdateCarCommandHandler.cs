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
        Car? car = await context.Cars
            .SingleOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (car is null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.Id));
        }
        
        // Usar servicio de caché para obtener marca
        Marca? marca = await cachedBrandService.GetByIdAsync(command.Marca, cancellationToken);

        if (marca is null)
        {
            return Result.Failure<Guid>(CarErrors.AtributesInvalid());
        }

        // Usar servicio de caché para obtener modelo
        Modelo? modelo = await cachedModelService.GetByIdAsync(command.Modelo, cancellationToken);

        if (modelo is null)
        {
            return Result.Failure<Guid>(CarErrors.AtributesInvalid());
        }


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
            command.Año,
            command.Patente,
            command.Descripcion,
            dateTimeProvider.UtcNow);
        
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

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(car.Id);
    }
}
