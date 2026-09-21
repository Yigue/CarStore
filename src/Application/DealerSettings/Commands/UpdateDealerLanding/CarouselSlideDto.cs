namespace Application.DealerSettings.Commands.UpdateDealerLanding;

/// <summary>Application-layer mirror of <see cref="Domain.DealerSettings.CarouselSlide"/>.</summary>
public sealed record CarouselSlideDto(
    string ImageUrl,
    string Title,
    string? Subtitle,
    string? Description,
    string? CtaText,
    string? CtaLink);
