using Application.Cars.Upload;
using Domain.Cars;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars;

internal sealed class Upload : IEndpoint
{
    public sealed class Request
    {
        public string carId { get; set; } // Asegúrate de que sea un GUID válido
        public IFormFile file { get; set; } // Archivo de imagen
        public bool isPrimary { get; set; } // Indica si es la imagen principal
        public int order { get; set; } // Orden de la imagen
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars/upload-image", async (
            [FromForm] Request request, // Usa [FromForm] para leer datos del formulario
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            if (request.file == null || request.file.Length == 0)
            {
                return Results.BadRequest("No file uploaded.");
            }

            // Leer el archivo en un MemoryStream
            using var memoryStream = new MemoryStream();
            await request.file.CopyToAsync(memoryStream, cancellationToken);

            // Crear el comando para subir la imagen
            // Guid.Parse will throw format exception if invalid, handled by GlobalExceptionHandler
            var command = new UploadImageCarCommand
            {
                CarId = Guid.Parse(request.carId), 
                ImageData = memoryStream.ToArray(), 
                FileName = request.file.FileName, 
                IsPrimary = request.isPrimary, 
                Order = request.order 
            };

            // Enviar el comando al manejador
            Result<Guid> result = await sender.Send(command, cancellationToken);

            // Devolver la respuesta
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("UploadCarImage")
        .Produces<Guid>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status500InternalServerError);

    }
}
