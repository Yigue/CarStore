using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Atribbutes;
using Domain.Cars.Events;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using SharedKernel;

namespace Application.Cars.Create;

internal sealed class CreateCarCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider
    )
    : ICommandHandler<CreateCarCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCarCommand command, CancellationToken cancellationToken)
    {
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
