using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class GetImageWithSas : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/{imageId}/url", async (
            [FromRoute] Guid imageId,
            IApplicationDbContext context,
            IBlobStorageService blobStorage,
            CancellationToken cancellationToken) =>
        {
            // Buscar la imagen por ID
            var image = await context.CarImages
                .FirstOrDefaultAsync(img => img.Id == imageId, cancellationToken);

            if (image == null)
            {
                return Results.NotFound($"Imagen con ID {imageId} no encontrada");
            }

            try
            {
                // Para URLs de Azure Blob Storage
                if (image.ImageUrl.Contains("blob.core.windows.net"))
                {
                    // Extraer el nombre del contenedor y el blob de la URL
                    var uri = new Uri(image.ImageUrl);
                    var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    if (pathSegments.Length < 2)
                    {
                        return Results.BadRequest("URL de imagen inválida");
                    }

                    string containerName = pathSegments[0];
                    string blobName = pathSegments[1];

                    // Verificar si el blob existe
                    bool exists = await blobStorage.ExistsAsync(containerName, blobName, cancellationToken);

                    if (!exists)
                    {
                        return Results.NotFound("La imagen no existe en el almacenamiento");
                    }

                    // Obtener el cliente de Azure Storage desde el servicio en lugar de crearlo aquí
                    var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(
                        new Uri($"{uri.Scheme}://{uri.Host}"));
                    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                    var blobClient = containerClient.GetBlobClient(blobName);

                    // Generar nueva URL con SAS
                    string newUrl = blobStorage.GenerateSasUri(blobClient).ToString();

                    return Results.Ok(new
                    {
                        imageId = image.Id,
                        carId = image.CarId,
                        url = newUrl,
                        isPrimary = image.IsPrimary
                    });
                }
                // Para URLs locales
                else
                {
                    // Simplemente devolver la URL tal cual está
                    return Results.Ok(new
                    {
                        imageId = image.Id,
                        carId = image.CarId,
                        url = image.ImageUrl,
                        isPrimary = image.IsPrimary
                    });
                }
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error al generar URL con SAS: {ex.Message}");
            }
        })
        .WithTags(Tags.Cars);
    }
}