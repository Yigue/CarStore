using Application.Abstractions.Authentication;
using Application.Abstractions.Tenancy;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Attributes;
using Domain.Cars.Events;
using Domain.Users;
using Application.Abstractions.Caching;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Create;

internal sealed class CreateCarCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ICachedBrandService cachedBrandService,
    ICachedModelService cachedModelService,
    ICurrentTenantService tenantService
    )
    : ICommandHandler<CreateCarCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCarCommand command, CancellationToken cancellationToken)
    {
        // Validate unique license plate before insert
        var licensePlate = new Domain.Shared.ValueObjects.LicensePlate(command.Patente);
        var existingCar = await context.Cars
            .IgnoreQueryFilters() // Patente única globalmente, no solo por concesionaria
            .AnyAsync(c => c.Patente == licensePlate, cancellationToken);

        if (existingCar)
        {
            return Result.Failure<Guid>(CarErrors.PatenteAlreadyExists(command.Patente));
        }

        // Usar servicio de caché para obtener marca
        var marcaDto = await cachedBrandService.GetByIdAsync(command.Marca, cancellationToken);

        if (marcaDto is null)
        {
            return Result.Failure<Guid>(CarErrors.AtributesInvalid());
        }
 
        // Usar servicio de caché para obtener modelo
        var modeloDto = await cachedModelService.GetByIdAsync(command.Modelo, cancellationToken);  

        if (modeloDto is null)
        {
            return Result.Failure<Guid>(CarErrors.AtributesInvalid());
        }

        var marca = await context.Marca.SingleAsync(m => m.Id == command.Marca, cancellationToken);
        var modelo = await context.Modelo.SingleAsync(m => m.Id == command.Modelo, cancellationToken);

        var car = new Car(
            tenantService.DealerId,
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
            command.Price,
            dateTimeProvider.UtcNow,
            command.FuelType,
            command.Featured,
            command.Transmission,
            command.PurchaseCost
            );

        context.Cars.Add(car);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(car.Id);
    }
}
