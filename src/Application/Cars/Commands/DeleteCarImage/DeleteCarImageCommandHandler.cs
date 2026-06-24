using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Cars.Commands.DeleteCarImage;

internal sealed class DeleteCarImageCommandHandler(
    IApplicationDbContext context,
    IStorageService storage,
    ICurrentTenantService tenant,
    ILogger<DeleteCarImageCommandHandler> logger)
    : ICommandHandler<DeleteCarImageCommand>
{
    public async Task<Result> Handle(DeleteCarImageCommand command, CancellationToken cancellationToken)
    {
        CarImage? image = await context.CarImages
            .Include(i => i.Car)
            .FirstOrDefaultAsync(
                i => i.Id == command.ImageId && i.CarId == command.CarId,
                cancellationToken);

        if (image is null)
        {
            return Result.Failure(CarErrors.ImageNotFound(command.ImageId));
        }

        // Tenant ownership check (defense in depth on top of any global query filter).
        if (image.Car is not null && image.Car.DealerId != tenant.DealerId)
        {
            return Result.Failure(CarErrors.ImageNotFound(command.ImageId));
        }

        // REQ-VMS-6: delete the blob first. DeleteFileAsync is idempotent (404 swallowed).
        // A non-404 storage failure aborts so DB and storage stay consistent.
        if (image.ObjectKey is not null)
        {
            try
            {
                await storage.DeleteFileAsync(image.ObjectKey, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to delete blob {ObjectKey} for image {ImageId}; aborting DB delete.",
                    image.ObjectKey, image.Id);
                return Result.Failure(CarErrors.BlobDeleteFailed);
            }
        }

        context.CarImages.Remove(image);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Blob already gone but row delete failed: log compensating-action warning (REQ-VMS-6).
            logger.LogWarning(ex,
                "Blob {ObjectKey} was deleted but the CarImage row {ImageId} could not be removed (orphan metadata).",
                image.ObjectKey, image.Id);
            throw;
        }

        return Result.Success();
    }
}
