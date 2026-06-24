using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Domain.Cars;
using Domain.Cars.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Cars.Delete;

internal sealed class DeleteCarCommandHandler(
    IApplicationDbContext context,
    IStorageService storage,
    ILogger<DeleteCarCommandHandler> logger)
    : ICommandHandler<DeleteCarCommand>
{
    public async Task<Result> Handle(DeleteCarCommand command, CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .Include(c => c.Images)
            .SingleOrDefaultAsync(t => t.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure(CarErrors.NotFound(command.CarId));
        }

        // REQ-VMS-5 / ADR-5: delete every MinIO blob BEFORE removing the Car. If any blob
        // delete fails (non-404), abort — the DB delete never runs, so DB and storage stay
        // consistent. DeleteFileAsync is idempotent (404 swallowed).
        var deletedKeys = new List<string>();
        foreach (CarImage image in car.Images.Where(i => i.ObjectKey is not null))
        {
            try
            {
                await storage.DeleteFileAsync(image.ObjectKey!, cancellationToken);
                deletedKeys.Add(image.ObjectKey!);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed deleting blob {ObjectKey} while deleting car {CarId}; aborting (DB not modified). " +
                    "Already-deleted blobs this run: {Deleted}.",
                    image.ObjectKey, car.Id, deletedKeys);
                return Result.Failure(CarErrors.CarBlobDeleteFailed);
            }
        }

        context.Cars.Remove(car);
        car.Raise(new CarDeleteDomainEvent(car.Id));

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Blobs already deleted but the DB delete failed: log a compensating-action warning.
            logger.LogWarning(ex,
                "Car {CarId} DB delete failed AFTER {Count} blob(s) were removed: {Deleted}. " +
                "These blobs are now orphaned and require manual/admin cleanup reconciliation.",
                car.Id, deletedKeys.Count, deletedKeys);
            throw;
        }

        return Result.Success();
    }
}
