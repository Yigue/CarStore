using Application.Abstractions.Data;
using Application.Abstractions.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class RegenerateImageUrls : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars/regenerate-image-urls", async (
            IApplicationDbContext context,
            IBlobStorageService blobStorage,
            CancellationToken cancellationToken) =>
        {
            // Obtener todas las imágenes
            var images = await context.CarImages
                .ToListAsync(cancellationToken);

            if (!images.Any())
            {
                return Results.Ok(new { message = "No hay imágenes para regenerar URLs" });
            }

            int updatedCount = 0;
            foreach (var image in images)
            {
                try
                {
                    // Solo procesar URLs de Azure Blob Storage
                    if (!image.ImageUrl.Contains("blob.core.windows.net"))
                    {
                        continue; // Saltar imágenes locales
                    }
                    
                    // Extraer el nombre del contenedor y el blob de la URL
                    var uri = new Uri(image.ImageUrl);
                    var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    
                    if (pathSegments.Length < 2)
                    {
                        continue;
                    }
                        
                    string containerName = pathSegments[0];
                    string blobName = pathSegments[1];
                    
                    // Verificar si el blob existe
                    bool exists = await blobStorage.ExistsAsync(containerName, blobName, cancellationToken);
                    if (!exists)
                    {
                        continue;
                    }
                    
                    // Obtener el cliente de Azure Storage
                    var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(
                        new Uri($"{uri.Scheme}://{uri.Host}"));
                    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                    var blobClient = containerClient.GetBlobClient(blobName);
                    
                    // Generar nueva URL con SAS
                    string newUrl = blobStorage.GenerateSasUri(blobClient).ToString();
                    
                    // Actualizar la URL en la base de datos
                    image.ImageUrl = newUrl;
                    updatedCount++;
                }
                catch (Exception)
                {
                    // Continuar con la siguiente imagen si hay un error
                }
            }
            
            // Guardar cambios
            await context.SaveChangesAsync(cancellationToken);
            
            return Results.Ok(new { 
                message = $"Se regeneraron {updatedCount} URLs de imágenes",
                updatedCount
            });
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("RegenerateCarImageUrls")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
} 