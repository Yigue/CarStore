namespace Application.Cars.Commands.UploadCarImage;

/// <summary>
/// Upload constraints for car images. These mirror the <c>Storage:Minio</c> config defaults
/// (AllowedContentTypes / MaxUploadSizeMb) and the gallery cap (REQ-CIU-8).
/// Kept in the Application layer so validators don't depend on Infrastructure options.
/// </summary>
public static class CarImageUploadConstraints
{
    public const int MaxImagesPerCar = 10;

    public const long MaxUploadSizeBytes = 10L * 1024 * 1024; // 10 MB

    public const int MaxFileNameLength = 255;

    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
        };

    /// <summary>Maps an allowed content type to its file extension (no dot). Defaults to <c>jpg</c>.</summary>
    public static string ExtensionFor(string contentType) => contentType?.ToLowerInvariant() switch
    {
        "image/png" => "png",
        "image/webp" => "webp",
        "image/jpeg" => "jpg",
        _ => "jpg",
    };
}
