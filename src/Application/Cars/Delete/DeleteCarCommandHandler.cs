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
    ILogger<DeleteCarCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
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

        // Five foreign keys reference Car with DeleteBehavior.Restrict — Appointment,
        // FinancialTransaction, Lead, Quote and Sale (pinned by CarReferenceDeleteBehaviorTests).
        // Each is commercial history that outlives the unit, so the database refuses the DELETE.
        // Discovering that by letting SaveChanges throw is what produced the original defect:
        // the blobs were already gone by then, so the operator got a 500, kept the vehicle, and
        // lost its photos. Ask first instead.
        if (await IsReferencedAsync(car.Id, cancellationToken))
        {
            // Withdraw rather than destroy, and keep the blobs: the referencing lead or quote
            // still renders this vehicle, and the row can be restored.
            car.SoftDelete(dateTimeProvider.UtcNow);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Car {CarId} is referenced by commercial records; withdrawn from circulation " +
                "instead of deleted. Its images were kept.",
                car.Id);

            return Result.Success();
        }

        // Nothing references it: the physical path REQ-VMS-5 / ADR-5 specifies still applies.
        // Blobs go first so a storage failure aborts before the DB is touched, leaving storage
        // and database consistent. DeleteFileAsync is idempotent (404 swallowed).
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

    /// <summary>
    /// True when any Restrict-mapped dependent still points at this vehicle. Kept in sync with
    /// <c>CarReferenceDeleteBehaviorTests.MustBlockDelete</c>: adding a blocking foreign key
    /// without adding it here would reintroduce the constraint-violation-after-blob-delete bug.
    /// </summary>
    private async Task<bool> IsReferencedAsync(Guid carId, CancellationToken cancellationToken) =>
        await context.Quotes.AnyAsync(q => q.CarId == carId, cancellationToken)
        || await context.Sales.AnyAsync(s => s.CarId == carId, cancellationToken)
        || await context.Leads.AnyAsync(l => l.InterestedVehicleId == carId, cancellationToken)
        || await context.Appointments.AnyAsync(a => a.VehicleId == carId, cancellationToken)
        || await context.Transactions.AnyAsync(t => t.CarId == carId, cancellationToken);
}
