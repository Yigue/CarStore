using Application.Abstractions.Data;
using Domain.Cars;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class GetImages : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("cars/{carId}/images", async (
            [FromRoute] Guid carId,
            IApplicationDbContext context,
            CancellationToken cancellationToken) =>
        {
            // Validar que el coche exista
            var car = await context.Cars
                .Include(c => c.Images)
                .FirstOrDefaultAsync(c => c.Id == carId, cancellationToken);

            if (car is null)
            {
                return Results.NotFound($"Coche con ID {carId} no encontrado");
            }

            // Obtener las imágenes ordenadas
            var images = car.Images
                .OrderBy(img => img.Order)
                .Select(img => new
                {
                    img.Id,
                    img.ImageUrl,
                    img.IsPrimary,
                    img.Order
                })
                .ToList();

            return Results.Ok(images);
        })
        .HasPermission(Permissions.CarsRead)
        .WithTags(Tags.Cars)
        .WithName("GetCarImages")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Endpoint para obtener todas las imágenes (no asociadas a un coche específico)
        app.MapGet("cars/images", async (
            IApplicationDbContext context,
            CancellationToken cancellationToken) =>
        {
            // Primero cargamos todas las imágenes
            var allImages = await context.CarImages
                .ToListAsync(cancellationToken);
                
            // Luego construimos la respuesta en memoria
            var images = allImages
                .Select(img => new
                {
                    img.Id,
                    img.CarId,
                    img.ImageUrl,
                    img.IsPrimary,
                    img.Order
                })
                .ToList();

            return Results.Ok(images);
        })
        .HasPermission(Permissions.CarsRead)
        .WithTags(Tags.Cars)
        .WithName("GetAllCarImages")
        .Produces(StatusCodes.Status200OK);
    }
} 