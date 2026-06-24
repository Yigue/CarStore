using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.CompleteReconditioningTask;

internal sealed class CompleteReconditioningTaskCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CompleteReconditioningTaskCommand>
{
    public async Task<Result> Handle(
        CompleteReconditioningTaskCommand command,
        CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .Include(c => c.ReconditioningTasks)
            .SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure(CarErrors.NotFound(command.CarId));
        }

        try
        {
            car.CompleteReconditioningTask(command.TaskId, dateTimeProvider.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.NotFound("Reconditioning.TaskNotFound", ex.Message));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
