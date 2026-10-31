using Application.Dealers.LandingImages.ConfirmLandingImageUpload;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Dealers.LandingImages;

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
        app.MapPost("dealer/landing-images/confirm", async (
            Request request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ConfirmLandingImageUploadCommand(
                request.imageId,
                request.fileName,
                request.contentType,
                request.sizeBytes);

            Result<LandingImageDto> result = await sender.Send(command, ct);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("ConfirmLandingImageUpload")
        .Produces<LandingImageDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
