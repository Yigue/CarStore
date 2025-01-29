using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Events;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
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
        var car = new Car(
            command.Marca,
            command.Modelo,
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
            dateTimeProvider.UtcNow
            );

        car.Raise(new NewCarDomainEvent(car.Id));

        context.Cars.Add(car);

        await context.SaveChangesAsync(cancellationToken);

        return car.Id;
    }
}
