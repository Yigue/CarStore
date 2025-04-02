using Application.Abstractions.Data;
using Microsoft.EntityFrameworkCore;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class GetPrimaryImages : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/primary-images", async (
            IApplicationDbContext context,
            CancellationToken cancellationToken) =>
        {
            // Primero cargamos todas las imágenes
            var allImages = await context.CarImages
                .ToListAsync(cancellationToken);
                
            // Luego filtramos y construimos la respuesta en memoria
            var primaryImages = allImages
                .Where(img => img.IsPrimary)
                .Select(img => new
                {
                    img.CarId,
                    img.Id,
                    img.ImageUrl
                })
                .ToList();

            return Results.Ok(primaryImages);
        })
        .WithTags(Tags.Cars);
    }
} 