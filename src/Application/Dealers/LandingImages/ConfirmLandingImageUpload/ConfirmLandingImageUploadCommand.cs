using Application.Abstractions.Messaging;

namespace Application.Dealers.LandingImages.ConfirmLandingImageUpload;

public sealed record ConfirmLandingImageUploadCommand(
    Guid ImageId,
    string FileName,
    string ContentType,
    long SizeBytes) : ICommand<LandingImageDto>;

/// <summary>Confirmed landing image: a presigned, time-limited read URL (same shape as <c>CarImageDto</c>).</summary>
public sealed record LandingImageDto(
    Guid ImageId,
    string Url,
    DateTime ExpiresAtUtc);
