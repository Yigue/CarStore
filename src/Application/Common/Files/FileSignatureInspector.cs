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
}
