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

                // Generar nueva URL con SAS - Nota: Esto asume que el servicio tiene acceso a la key
                // Idealmente el BlobStorageService debería encapsular esto completamente
                // Por ahora replicamos la lógica existente pero limpia
                
                // TODO: Refactor BlobStorageService to expose GenerateSas(container, blob) directly
                // For now, we manually reconstruct client as in the original endpoint
                
                var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(
                    new Uri($"{uri.Scheme}://{uri.Host}"));
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);

                string newUrl = blobStorage.GenerateSasUri(blobClient).ToString();

                image.ImageUrl = newUrl;
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
