namespace Domain.DealerSettings;

/// <summary>
/// CFG-06: one slide of the dealership's landing-page hero carousel.
///
/// <para>
/// Persisted as a JSON array on <see cref="DealerSettings.CarouselSlidesJson"/> rather than as a
/// child table — a carousel is an ordered list a single admin edits as a whole (add a slide,
/// reorder, delete), never queried or joined against, so a table and its migrations would buy
/// nothing a JSON column doesn't already give for free. Immutable record: the whole array is
/// replaced on every save, matching how the landing editor actually works (see
/// <see cref="Domain.DealerSettings.DealerSettings.UpdateLanding"/>).
/// </para>
/// </summary>
public sealed record CarouselSlide(
    string ImageUrl,
    string Title,
    string? Subtitle,
    string? Description,
    string? CtaText,
    string? CtaLink);
