using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Cars.Events;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Delete;

internal sealed class DeleteCarCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteCarCommand>
{
    public async Task<Result> Handle(DeleteCarCommand command, CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .SingleOrDefaultAsync(t => t.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure(CarErrors.NotFound(command.CarId));
        }

        context.Cars.Remove(car);

        car.Raise(new CarDeleteDomainEvent(car.Id));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
