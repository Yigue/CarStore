namespace Application.Cars.Queries.GetCarImages;

/// <summary>
/// Read model for a car image. <see cref="Url"/> is a presigned (public-host) URL when the
/// image is MinIO-backed, or the legacy direct URL otherwise. <see cref="ExpiresAt"/> is the
/// presigned URL expiry (or <see cref="DateTime.MaxValue"/> for legacy URLs).
/// </summary>
public sealed record CarImageDto(
    Guid Id,
    string Url,
    bool IsCover,
    int DisplayOrder,
    DateTime ExpiresAt);
