using Application.Cars.Upload;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class UploadImage : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars/{carId}/images", async (
            [FromRoute] Guid carId,
            [FromForm] IFormFile file,
            [FromForm] bool isPrimary,
            [FromForm] int order,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded");
            }

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
                
            var command = new UploadImageCarCommand
            {
                CarId = carId,
                FileName = file.FileName,
                ImageData = memoryStream.ToArray(),
                IsPrimary = isPrimary,
                Order = order
            };

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                id => Results.Created($"/api/cars/{carId}/images/{id}", id),
                CustomResults.Problem);
        })
        .DisableAntiforgery() // Often needed for file uploads if not using standard strict CSRF
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("UploadCarImageRoute")
        .Produces<Guid>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
