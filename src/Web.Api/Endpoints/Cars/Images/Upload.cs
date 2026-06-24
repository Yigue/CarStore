using Application.Cars.Commands.UploadCarImage;
using Application.Cars.Queries.GetCarImages;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class Upload : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars/{carId:guid}/images", async (
            [FromRoute] Guid carId,
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded.");
            }

            await using Stream stream = file.OpenReadStream();

            var command = new UploadCarImageCommand(
                carId,
                stream,
                file.ContentType,
                file.FileName,
                file.Length);

            Result<CarImageDto> result = await sender.Send(command, ct);

            return result.Match(
                dto => Results.Created($"/api/v1/cars/{carId}/images/{dto.Id}", dto),
                CustomResults.Problem);
        })
        .DisableAntiforgery()
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("UploadCarImageToStorage")
        .Produces<CarImageDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
