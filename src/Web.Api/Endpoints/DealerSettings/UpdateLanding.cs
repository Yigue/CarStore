using Application.DealerSettings.Commands.UpdateDealerLanding;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.DealerSettings;

/// <summary>
/// CFG-06: lets a dealership rewrite its own landing page — history, mission, vision, values and
/// the hero carousel — instead of shipping every dealer the same hardcoded copy.
/// </summary>
internal sealed class UpdateLanding : IEndpoint
{
    public sealed record SlideRequest(
        string ImageUrl,
        string Title,
        string? Subtitle,
        string? Description,
        string? CtaText,
        string? CtaLink);

    public sealed record Request(
        string? HistoryText,
        string? MissionText,
        string? VisionText,
        string? ValuesText,
        IReadOnlyList<SlideRequest>? CarouselSlides);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("dealer-settings/landing", async (
            Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            // Named arguments (ArchitectureTests.CommandConstructionTests): four string?
            // parameters in a row is exactly the shape where a swapped pair compiles silently
            // and ships Historia's text into the Misión field.
            var command = new UpdateDealerLandingCommand(
                HistoryText: request.HistoryText,
                MissionText: request.MissionText,
                VisionText: request.VisionText,
                ValuesText: request.ValuesText,
                CarouselSlides: request.CarouselSlides?
                    .Select(s => new CarouselSlideDto(s.ImageUrl, s.Title, s.Subtitle, s.Description, s.CtaText, s.CtaLink))
                    .ToList());

            Result<Guid> result = await sender.Send(command, cancellationToken);

            return result.Match(
                _ => Results.Ok(),
                CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags(Tags.DealerSettings)
        .WithName("UpdateDealerLanding")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
