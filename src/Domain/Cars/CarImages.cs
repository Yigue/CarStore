using SharedKernel;

namespace Domain.Cars;

/// <summary>
/// A vehicle image. Dual-mode (ADR-2 / ADR-6):
/// <list type="bullet">
/// <item>If <see cref="ObjectKey"/> is not null → the blob lives in MinIO and the read URL is a presigned URL.</item>
/// <item>Otherwise → legacy image, served directly via <see cref="ImageUrl"/>.</item>
/// </list>
/// </summary>
public class CarImage : Entity
{
    public Guid CarId { get; private set; }

    /// <summary>Legacy direct URL (Azure blob / local). Nullable: new MinIO images use <see cref="ObjectKey"/> instead.</summary>
    public string? ImageUrl { get; private set; }

    /// <summary>MinIO object key (<c>cars/{dealerId}/{carId}/{imageId}.{ext}</c>). Null for legacy images.</summary>
    public string? ObjectKey { get; private set; }

    /// <summary>MIME content type of the stored blob (e.g. <c>image/jpeg</c>). Null for legacy images.</summary>
    public string? ContentType { get; private set; }

    /// <summary>Size of the stored blob in bytes. Null for legacy images.</summary>
    public long? SizeBytes { get; private set; }

    /// <summary>True when this image is the cover. Exactly one cover per car is enforced (REQ-VMS-7).</summary>
    public bool IsCover { get; private set; }

    /// <summary>Zero-based display order within the car's gallery.</summary>
    public int DisplayOrder { get; private set; }

    public Car Car { get; private set; }

    private CarImage()
    {
    }

    /// <summary>
    /// Legacy factory: image served directly via <paramref name="imageUrl"/> (no MinIO object).
    /// </summary>
    public CarImage(Guid carId, string imageUrl, bool isCover, int displayOrder)
    {
        Id = Guid.NewGuid();
        CarId = carId;
        ImageUrl = imageUrl;
        IsCover = isCover;
        DisplayOrder = displayOrder;
    }

    /// <summary>
    /// Modern factory for MinIO-backed images (REQ-VMS-8). The <paramref name="objectKey"/> follows
    /// <c>cars/{dealerId}/{carId}/{imageId}.{ext}</c>.
    /// </summary>
    public static CarImage Create(
        Guid imageId,
        Guid carId,
        string objectKey,
        string contentType,
        long sizeBytes,
        int displayOrder,
        bool isCover = false)
    {
        return new CarImage
        {
            Id = imageId,
            CarId = carId,
            ObjectKey = objectKey,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            DisplayOrder = displayOrder,
            IsCover = isCover,
            ImageUrl = null,
        };
    }

    public void SetAsCover(bool isCover)
    {
        IsCover = isCover;
    }

    // --- Backward-compatibility shims (legacy code used IsPrimary / Order) ---
    // These are NOT mapped to extra columns; they alias the new canonical properties.

    /// <summary>Legacy alias of <see cref="IsCover"/>. Kept so pre-Phase-2 consumers keep compiling.</summary>
    public bool IsPrimary => IsCover;

    /// <summary>Legacy alias of <see cref="DisplayOrder"/>.</summary>
    public int Order => DisplayOrder;

    /// <summary>Legacy alias of <see cref="SetAsCover(bool)"/>.</summary>
    public void SetAsPrimary(bool isPrimary) => SetAsCover(isPrimary);

    public void SetDisplayOrder(int displayOrder)
    {
        DisplayOrder = displayOrder;
    }

    public void UpdateImageUrl(string newUrl)
    {
        ImageUrl = newUrl;
    }
}
