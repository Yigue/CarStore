using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Cars.Commands.UploadCarImage;
using Application.Cars.Queries.GetCarImages;
using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Cars.Commands.ConfirmImageUpload;

internal sealed class ConfirmImageUploadCommandHandler(
    IApplicationDbContext context,
    IStorageService storage,
    ICurrentTenantService tenant,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ConfirmImageUploadCommand, CarImageDto>
{
    private static readonly TimeSpan ReadTtl = TimeSpan.FromMinutes(15);

    public async Task<Result<CarImageDto>> Handle(
        ConfirmImageUploadCommand command,
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

        if (string.IsNullOrWhiteSpace(command.ContentType) ||
            !CarImageUploadConstraints.AllowedContentTypes.Contains(command.ContentType))
        {
            return Result.Failure<CarImageDto>(
                Error.Validation("CarImage.InvalidContentType", "Content type must be one of: image/jpeg, image/png, image/webp."));
        }

        if (command.SizeBytes <= 0 || command.SizeBytes > CarImageUploadConstraints.MaxUploadSizeBytes)
        {
            return Result.Failure<CarImageDto>(
                Error.Validation("CarImage.InvalidSize", "Image exceeds the maximum allowed size (10 MB)."));
        }

        string ext = Path.GetExtension(command.FileName).TrimStart('.');
        if (string.IsNullOrEmpty(ext)) ext = "jpg";
        string objectKey = $"{tenant.DealerId}/{car.Id}/{command.ImageId}.{ext}";

        // We assume the file is already in storage because the client uploaded it.
        // In a more robust system, we would verify its existence here.

        int nextOrder = car.Images.Count == 0
            ? 0
            : car.Images.Max(i => i.DisplayOrder) + 1;

        bool isCover = car.Images.Count == 0;

        CarImage image = CarImage.Create(
            command.ImageId,
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
