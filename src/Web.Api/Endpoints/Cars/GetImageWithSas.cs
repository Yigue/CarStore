using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
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
                string imageUrl = image.ImageUrl ?? string.Empty;

                if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out Uri? uri) ||
                    !uri.Host.Contains("blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Ok(new
                    {
                        imageId = image.Id,
                        carId = image.CarId,
                        url = imageUrl,
                        isPrimary = image.IsPrimary
                    });
                }

                string[] pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (pathSegments.Length < 2)
                {
                    return Results.BadRequest("URL de imagen inválida");
                }

                string containerName = pathSegments[0];
                string blobName = string.Join('/', pathSegments.Skip(1));

                bool exists = await blobStorage.ExistsAsync(containerName, blobName, cancellationToken);

                if (!exists)
                {
                    return Results.NotFound("La imagen no existe en el almacenamiento");
                }

                string newUrl = await blobStorage.GenerateAccessUrlAsync(containerName, blobName, cancellationToken);

                return Results.Ok(new
                {
                    imageId = image.Id,
                    carId = image.CarId,
                    url = newUrl,
                    isPrimary = image.IsPrimary
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error al generar URL con SAS: {ex.Message}");
            }
        })
        .WithTags(Tags.Cars);
    }
}