namespace Application.DealerSettings;

public sealed record DealerSettingsResponse
{
    public Guid Id { get; init; }
    public Guid DealerId { get; init; }
    public string DealerName { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public bool NotificationsEnabled { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? HostName { get; init; }
    public string? Slug { get; init; }
    public bool IsActive { get; init; }
    public string? CustomDomain { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? FacebookUrl { get; init; }
    public string? InstagramUrl { get; init; }
    public string? TwitterUrl { get; init; }
    public decimal? InterestRateTna { get; init; }
    // Visual settings
    public string? LogoUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
    public string? FooterText { get; init; }

    // CFG-06: landing page customization. The public landing page (AllowAnonymous — this DTO
    // is what GET /dealer-settings serves it) reads these instead of the hardcoded copy.
    public string? HistoryText { get; init; }
    public string? MissionText { get; init; }
    public string? VisionText { get; init; }
    public string? ValuesText { get; init; }
    public IReadOnlyList<CarouselSlideResponse> CarouselSlides { get; init; } = [];
}

/// <summary>Wire shape of a hero-carousel slide, mirroring <see cref="Domain.DealerSettings.CarouselSlide"/>.</summary>
public sealed record CarouselSlideResponse(
    string ImageUrl,
    string Title,
    string? Subtitle,
    string? Description,
    string? CtaText,
    string? CtaLink);
