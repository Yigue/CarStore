using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Cars.Queries.GetCarImages;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.UploadCarImage;

internal sealed class UploadCarImageCommandHandler(
    IApplicationDbContext context,
    IStorageService storage,
    ICurrentTenantService tenant,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UploadCarImageCommand, CarImageDto>
{
    private static readonly TimeSpan ReadTtl = TimeSpan.FromMinutes(15);

    public async Task<Result<CarImageDto>> Handle(
        UploadCarImageCommand command,
        CancellationToken cancellationToken)
    {
        Car? car = await context.Cars
            .Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == command.CarId, cancellationToken);

        if (car is null)
        {
            return Result.Failure<CarImageDto>(CarErrors.NotFound(command.CarId));
        }

        if (car.Images.Count >= CarImageUploadConstraints.MaxImagesPerCar)
        {
            return Result.Failure<CarImageDto>(
                CarErrors.ImageLimitReached(CarImageUploadConstraints.MaxImagesPerCar));
        }

        Guid imageId = Guid.NewGuid();
        string ext = CarImageUploadConstraints.ExtensionFor(command.ContentType);
        string objectKey = $"{tenant.DealerId}/{car.Id}/{imageId}.{ext}";

        await storage.UploadFileAsync(
            command.FileStream,
            objectKey,
            command.ContentType,
            command.SizeBytes,
            cancellationToken);

        int nextOrder = car.Images.Count == 0
            ? 0
            : car.Images.Max(i => i.DisplayOrder) + 1;

        bool isCover = car.Images.Count == 0; // first image becomes the cover by default

        CarImage image = CarImage.Create(
            imageId,
            car.Id,
            objectKey,
            command.ContentType,
            command.SizeBytes,
            nextOrder,
            isCover);

        car.Images.Add(image);
        context.CarImages.Add(image);

        await context.SaveChangesAsync(cancellationToken);

        Uri url = await storage.GetPresignedUrlAsync(objectKey, ReadTtl, cancellationToken);

        var dto = new CarImageDto(
            image.Id,
            url.ToString(),
            image.IsCover,
            image.DisplayOrder,
            dateTimeProvider.UtcNow.Add(ReadTtl));

        return Result.Success(dto);
    }
}
