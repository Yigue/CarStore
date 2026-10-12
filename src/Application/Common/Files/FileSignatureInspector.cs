namespace Application.Common.Files;

/// <summary>
/// Magic-byte check for uploaded images (RN-035, H11). The declared Content-Type is client-controlled,
/// so the real file signature is the only trustworthy evidence of the format.
/// </summary>
public static class FileSignatureInspector
{
    /// <summary>Number of leading bytes needed to recognise every supported signature.</summary>
    public const int HeadLength = 16;

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> RiffSignature => [0x52, 0x49, 0x46, 0x46];

    private static ReadOnlySpan<byte> WebpSignature => [0x57, 0x45, 0x42, 0x50];

    /// <summary>True when <paramref name="head"/> starts with a JPEG, PNG or WebP signature.</summary>
    public static bool IsSupportedImage(ReadOnlySpan<byte> head) =>
        head.StartsWith(JpegSignature)
        || head.StartsWith(PngSignature)
        || (head.Length >= 12 && head.StartsWith(RiffSignature) && head.Slice(8, 4).SequenceEqual(WebpSignature));

    /// <summary>
    /// Content type derived from the file signature (<c>image/jpeg</c>, <c>image/png</c>, <c>image/webp</c>),
    /// or null when the bytes are not a supported image. Only the leading bytes are inspected: content
    /// after the signature (e.g. a polyglot carrying HTML/SVG) is NOT scanned.
    /// </summary>
    public static string? Detect(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith(JpegSignature))
        {
            return "image/jpeg";
        }

        if (head.StartsWith(PngSignature))
        {
            return "image/png";
        }

        if (head.Length >= 12 && head.StartsWith(RiffSignature) && head.Slice(8, 4).SequenceEqual(WebpSignature))
        {
            return "image/webp";
        }

        return null;
    }

    /// <summary>True when <paramref name="extension"/> (no dot, any case) is valid for the detected type.</summary>
    public static bool IsExtensionCompatible(string detectedContentType, string extension) =>
        detectedContentType switch
        {
            "image/jpeg" => extension.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                            || extension.Equals("jpeg", StringComparison.OrdinalIgnoreCase),
            "image/png" => extension.Equals("png", StringComparison.OrdinalIgnoreCase),
            "image/webp" => extension.Equals("webp", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
}
