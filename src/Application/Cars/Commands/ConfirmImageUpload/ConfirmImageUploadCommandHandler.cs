using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Cars.Commands.UploadCarImage;
using Application.Cars.Queries.GetCarImages;
using Application.Common.Files;
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

        // The declared Content-Type is client-controlled: verify the real signature of the stored
        // object (A5.1 / H11) and discard it when it is missing or not a supported image.
        byte[]? head = await storage.ReadHeadAsync(objectKey, FileSignatureInspector.HeadLength, cancellationToken);
        string? detectedType = head is null ? null : FileSignatureInspector.Detect(head);

        // The stored type is the DETECTED one. A declared type or key extension that disagrees with the
        // signature is not reconciled: the object is rejected and deleted (never trust the client).
        if (detectedType is null
            || !string.Equals(detectedType, command.ContentType, StringComparison.OrdinalIgnoreCase)
            || !FileSignatureInspector.IsExtensionCompatible(detectedType, ext))
        {
            await storage.DeleteFileAsync(objectKey, cancellationToken);
            return Result.Failure<CarImageDto>(
                Error.Validation("CarImage.InvalidSignature", "The uploaded file is not a valid JPEG, PNG or WebP image, or its type or extension does not match its content."));
        }

        int nextOrder = car.Images.Count == 0
            ? 0
            : car.Images.Max(i => i.DisplayOrder) + 1;

        bool isCover = car.Images.Count == 0;

        CarImage image = CarImage.Create(
            command.ImageId,
            car.Id,
            objectKey,
            detectedType,
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
