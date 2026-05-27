using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.AddReconditioningTask;

internal sealed class AddReconditioningTaskCommandHandler(
    IApplicationDbContext context)
    : ICommandHandler<AddReconditioningTaskCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        AddReconditioningTaskCommand command,
        CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .Include(c => c.ReconditioningTasks)
            .SingleOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure<Guid>(CarErrors.NotFound(command.CarId));
        }

        ReconditioningTask task = car.AddReconditioningTask(
            command.Description,
            new Money(command.Cost, command.Currency));

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(task.Id);
    }
}
