namespace Application.Dealers.LandingImages;

/// <summary>
/// Upload constraints for dealer landing-page images (A5.1, A5.2). Mirrors
/// <c>CarImageUploadConstraints</c> but with a tighter size cap: a landing hero/banner slide
/// is a decorative asset, not an evidentiary document, so 5 MB is a reasonable ceiling instead
/// of the 10 MB allowed for car galleries.
/// </summary>
public static class LandingImageUploadConstraints
{
    public const long MaxUploadSizeBytes = 5L * 1024 * 1024; // 5 MB

    public const int MaxFileNameLength = 255;

    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
        };
}
