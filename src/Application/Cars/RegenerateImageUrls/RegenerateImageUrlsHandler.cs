using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Application.Cars.RegenerateImageUrls;

internal sealed class RegenerateImageUrlsHandler(
    IApplicationDbContext context,
    IBlobStorageService blobStorage,
    ILogger<RegenerateImageUrlsHandler> logger
) : IRequestHandler<RegenerateImageUrlsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(
        RegenerateImageUrlsCommand request,
        CancellationToken cancellationToken)
    {
        var images = await context.CarImages
            .ToListAsync(cancellationToken);

        if (images.Count == 0)
        {
            return Result.Success(0);
        }

        int updatedCount = 0;

        foreach (var image in images)
        {
            try
            {
                if (!image.ImageUrl.Contains("blob.core.windows.net"))
                {
                    continue;
                }

                var uri = new Uri(image.ImageUrl);
                var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (pathSegments.Length < 2)
                {
                    logger.LogWarning("Invalid image URL format for ImageId {ImageId}: {Url}", image.Id, image.ImageUrl);
                    continue;
                }

                string containerName = pathSegments[0];
                string blobName = pathSegments[1];

                bool exists = await blobStorage.ExistsAsync(containerName, blobName, cancellationToken);
                if (!exists)
                {
                    logger.LogWarning("Blob not found for ImageId {ImageId}: {Container}/{Blob}", image.Id, containerName, blobName);
                    continue;
                }

                string newUrl = blobStorage.GenerateSasUrl(containerName, blobName);

                image.UpdateImageUrl(newUrl);
                updatedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to regenerate URL for ImageId {ImageId}", image.Id);
                // Continue with next image, but at least we logged it!
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(updatedCount);
    }
}
