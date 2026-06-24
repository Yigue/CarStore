using Application.Cars.Commands.ConfirmImageUpload;
using Application.Cars.Queries.GetCarImages;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class ConfirmUpload : IEndpoint
{
    public sealed class Request
    {
        public Guid imageId { get; init; }
        public string fileName { get; init; } = string.Empty;
        public string contentType { get; init; } = string.Empty;
        public long sizeBytes { get; init; }
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars/{carId:guid}/images/confirm", async (
            Guid carId,
            Request request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ConfirmImageUploadCommand(
                carId,
                request.imageId,
                request.fileName,
                request.contentType,
                request.sizeBytes);

            Result<CarImageDto> result = await sender.Send(command, ct);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("ConfirmCarImageUpload")
        .Produces<CarImageDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
