using Application.Abstractions.Messaging;

namespace Application.DealerSettings.Commands.UpdateDealerLanding;

/// <summary>
/// CFG-06: replaces the dealership's landing-page copy and hero carousel. A full replacement,
/// not a patch — the editor screen sends what the landing should say from now on, and
/// <c>DealerSettings.UpdateLanding</c> is what makes that atomic.
/// </summary>
public sealed record UpdateDealerLandingCommand(
    string? HistoryText,
    string? MissionText,
    string? VisionText,
    string? ValuesText,
    IReadOnlyList<CarouselSlideDto>? CarouselSlides
) : ICommand<Guid>;
