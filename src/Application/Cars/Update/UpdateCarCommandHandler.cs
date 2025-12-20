using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Update;

internal sealed class UpdateCarCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
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
        Marca marca = await context.Marca
           .SingleOrDefaultAsync(m => m.Id == command.Marca, cancellationToken);

        if (marca is null)
        {
            return Result.Failure<Guid>(CarErrors.AtributesInvalid());
        }

        Modelo modelo = await context.Modelo
            .SingleOrDefaultAsync(m => m.Id == command.Modelo, cancellationToken);

        if (modelo is null)
        {
            return Result.Failure<Guid>(CarErrors.AtributesInvalid());
        }


        // Update properties that need to be public for EF Core
        car.Marca = marca;
        car.Modelo = modelo;
        car.Color = command.Color;
        car.CarType = command.CarType;
        car.CarStatus = command.CarStatus;
        car.ServiceCar = command.ServiceCar;
        car.CantidadPuertas = command.CantidadPuertas;
        car.CantidadAsientos = command.CantidadAsientos;
        car.Cilindrada = command.Cilindrada;
        car.Kilometraje = command.Kilometraje;
        car.Año = command.Año;
        car.Patente = new LicensePlate(command.Patente);
        car.Descripcion = command.Descripcion;
        
        // Use domain method for price update
        if (car.Price.Amount != command.Price)
        {
            car.UpdatePrice(command.Price);
        }
        
        // Handle service status changes using domain methods
        if (command.ServiceCar == statusServiceCar.Vendido && car.ServiceCar != statusServiceCar.Vendido)
        {
            car.MarkAsSold();
        }
        else if (command.ServiceCar == statusServiceCar.Disponible && car.ServiceCar != statusServiceCar.Disponible)
        {
            car.MarkAsAvailable();
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(car.Id);
    }
}
