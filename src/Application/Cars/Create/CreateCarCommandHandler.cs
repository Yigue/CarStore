using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Cars.Events;
using Domain.Users;
using Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SharedKernel;

namespace Application.Cars.Create;

internal sealed class CreateCarCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    CachedBrandService cachedBrandService,
    CachedModelService cachedModelService
    )
    : ICommandHandler<CreateCarCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCarCommand command, CancellationToken cancellationToken)
    {
        // Validate unique license plate before insert
        var licensePlate = new Domain.Shared.ValueObjects.LicensePlate(command.Patente);
        var existingCar = await context.Cars
            .AnyAsync(c => c.Patente.Value == licensePlate.Value, cancellationToken);

        if (existingCar)
        {
            return Result.Failure<Guid>(CarErrors.PatenteAlreadyExists(command.Patente));
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


        var car = new Car(
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
            command.Price,
            dateTimeProvider.UtcNow
            );

        context.Cars.Add(car);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(car.Id);
    }
}
