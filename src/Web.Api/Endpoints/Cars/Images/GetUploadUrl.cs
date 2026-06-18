using Application.Cars.Commands.GetImageUploadUrl;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Cars.Images;

internal sealed class GetUploadUrl : IEndpoint
{
    public sealed class Request
    {
        public string fileName { get; init; } = string.Empty;
        public string contentType { get; init; } = string.Empty;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("cars/{carId:guid}/images/upload-url", async (
            Guid carId,
            Request request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new GetImageUploadUrlCommand(carId, request.fileName, request.contentType);

            Result<ImageUploadUrlResponse> result = await sender.Send(command, ct);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(Permissions.CarsUpdate)
        .WithTags(Tags.Cars)
        .WithName("GetCarImageUploadUrl")
        .Produces<ImageUploadUrlResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
