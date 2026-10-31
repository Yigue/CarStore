using Application.Dealers.LandingImages.GetLandingImageUploadUrl;
using MediatR;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Dealers.LandingImages;

internal sealed class GetUploadUrl : IEndpoint
{
    public sealed class Request
    {
        public string fileName { get; init; } = string.Empty;
        public string contentType { get; init; } = string.Empty;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("dealer/landing-images/upload-url", async (
            Request request,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new GetLandingImageUploadUrlCommand(request.fileName, request.contentType);

            Result<LandingImageUploadUrlResponse> result = await sender.Send(command, ct);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("GetLandingImageUploadUrl")
        .Produces<LandingImageUploadUrlResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
