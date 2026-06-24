using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.SetCoverImage;

internal sealed class SetCoverImageCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenant)
    : ICommandHandler<SetCoverImageCommand>
{
    public async Task<Result> Handle(SetCoverImageCommand command, CancellationToken cancellationToken)
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

        CarImage? target = car.Images.FirstOrDefault(i => i.Id == command.ImageId);
        if (target is null)
        {
            return Result.Failure(CarErrors.ImageNotFoundInCar(command.ImageId, command.CarId));
        }

        // Demote every current cover BEFORE promoting the target, so the partial unique index
        // (one cover per car) is never violated mid-transaction. Single SaveChanges = atomic.
        foreach (CarImage image in car.Images.Where(i => i.IsCover && i.Id != target.Id))
        {
            image.SetAsCover(false);
        }

        target.SetAsCover(true);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
