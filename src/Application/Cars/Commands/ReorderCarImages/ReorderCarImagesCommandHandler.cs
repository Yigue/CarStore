using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.ReorderCarImages;

internal sealed class ReorderCarImagesCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenant)
    : ICommandHandler<ReorderCarImagesCommand>
{
    public async Task<Result> Handle(ReorderCarImagesCommand command, CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure(CarErrors.NotFound(command.CarId));
        }

        if (car.DealerId != tenant.DealerId)
        {
            return Result.Failure(CarErrors.NotFound(command.CarId));
        }

        var existingIds = car.Images.Select(i => i.Id).ToHashSet();

        // The supplied set MUST match the car's image set exactly (same cardinality, same ids).
        if (command.OrderedImageIds.Count != existingIds.Count ||
            !command.OrderedImageIds.All(existingIds.Contains))
        {
            return Result.Failure(CarErrors.ReorderMismatch());
        }

        for (int index = 0; index < command.OrderedImageIds.Count; index++)
        {
            Guid id = command.OrderedImageIds[index];
            CarImage image = car.Images.First(i => i.Id == id);
            image.SetDisplayOrder(index);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
