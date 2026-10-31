using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using Application.Common.Files;
using SharedKernel;

namespace Application.Dealers.LandingImages.ConfirmLandingImageUpload;

/// <summary>
/// A5.1 / A16 / RN-035 (closes H11 for landing images too, same as <c>ConfirmImageUploadCommandHandler</c>
/// does for car images): confirms a landing-carousel image already PUT into object storage via the
/// presigned URL from <c>GetLandingImageUploadUrlCommand</c>.
/// <para>
/// Dealer scoping is structural, not a lookup: the object key is always rebuilt from THIS caller's
/// own <see cref="ICurrentTenantService.DealerId"/> — a client can never point this command at
/// another dealer's blob, because it can never spell another dealer's key.
/// </para>
/// <para>
/// This command does not write to <c>DealerSettings</c> itself: a carousel slide also needs a title
/// (<c>DealerSettings.UpdateLanding</c> rejects an imageless OR titleless slide), which doesn't exist
/// yet at upload time. Confirming only proves the object is a genuine, policy-compliant image and
/// hands back a durable URL; the dealer's existing <c>PUT dealer-settings/landing</c> (CFG-06) is what
/// actually writes that URL into the landing config, as part of a complete slide.
/// </para>
/// </summary>
internal sealed class ConfirmLandingImageUploadCommandHandler(
    IStorageService storage,
    ICurrentTenantService tenant,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ConfirmLandingImageUploadCommand, LandingImageDto>
{
    private static readonly TimeSpan ReadTtl = TimeSpan.FromMinutes(15);

    public async Task<Result<LandingImageDto>> Handle(
        ConfirmLandingImageUploadCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.ContentType) ||
            !LandingImageUploadConstraints.AllowedContentTypes.Contains(command.ContentType))
        {
            return Result.Failure<LandingImageDto>(
                Error.Validation("LandingImage.InvalidContentType", "Content type must be one of: image/jpeg, image/png, image/webp."));
        }

        // Fast, cheap rejection of an obviously-bad request — but NEVER the authoritative check: a
        // presigned POST URL only constrains bucket/key/content-type/expiry, not size (see
        // IStorageService.GeneratePresignedPostAsync), so a client can assert any SizeBytes it wants
        // while actually uploading a much larger object. The real check is below, against storage.
        if (command.SizeBytes <= 0 || command.SizeBytes > LandingImageUploadConstraints.MaxUploadSizeBytes)
        {
            return Result.Failure<LandingImageDto>(
                Error.Validation("LandingImage.InvalidSize", "Image exceeds the maximum allowed size (5 MB)."));
        }

        string ext = Path.GetExtension(command.FileName).TrimStart('.');
        if (string.IsNullOrEmpty(ext)) ext = "jpg";
        string objectKey = $"landing/{tenant.DealerId}/{command.ImageId}.{ext}";

        // Security review (sfc A5.1): re-check the size MinIO actually stored, not the client's claim.
        long? actualSize = await storage.GetObjectSizeAsync(objectKey, cancellationToken);
        if (actualSize is null)
        {
            return Result.Failure<LandingImageDto>(
                Error.Validation("LandingImage.NotFound", "No upload was found for this image. Upload the file to the presigned URL first."));
        }

        if (actualSize.Value > LandingImageUploadConstraints.MaxUploadSizeBytes)
        {
            await storage.DeleteFileAsync(objectKey, cancellationToken);
            return Result.Failure<LandingImageDto>(
                Error.Validation("LandingImage.InvalidSize", "Image exceeds the maximum allowed size (5 MB)."));
        }

        // The declared Content-Type is client-controlled: verify the real signature of the stored
        // object (A5.1 / H11) and discard it when it is missing or not a supported image. A renamed
        // .exe claiming to be a .jpg must not pass just because the extension and declared type agree.
        byte[]? head = await storage.ReadHeadAsync(objectKey, FileSignatureInspector.HeadLength, cancellationToken);
        string? detectedType = head is null ? null : FileSignatureInspector.Detect(head);

        if (detectedType is null
            || !string.Equals(detectedType, command.ContentType, StringComparison.OrdinalIgnoreCase)
            || !FileSignatureInspector.IsExtensionCompatible(detectedType, ext))
        {
            await storage.DeleteFileAsync(objectKey, cancellationToken);
            return Result.Failure<LandingImageDto>(
                Error.Validation("LandingImage.InvalidSignature", "The uploaded file is not a valid JPEG, PNG or WebP image, or its type or extension does not match its content."));
        }

        // Security review (sfc A5.1): TOCTOU — the presigned POST URL from GetLandingImageUploadUrl
        // stays valid (and re-writable to this SAME key) for its own 10-minute TTL, which outlives
        // this one-time check. Without this step, a caller could re-upload a different (e.g.
        // oversized) object to objectKey after a successful Confirm, and nothing would ever
        // re-validate it — the dealer's landing config would already be pointing at that key. Move
        // the validated bytes to a key under a DIFFERENT prefix that no presigned POST was ever
        // issued for, then destroy the original (still-writable) upload key so it can no longer be
        // served even if something does manage to write to it again.
        string confirmedKey = $"landing-confirmed/{tenant.DealerId}/{command.ImageId}.{ext}";
        await using (Stream? validated = await storage.OpenReadAsync(objectKey, cancellationToken))
        {
            if (validated is null)
            {
                return Result.Failure<LandingImageDto>(
                    Error.Validation("LandingImage.NotFound", "No upload was found for this image. Upload the file to the presigned URL first."));
            }

            await storage.UploadFileAsync(validated, confirmedKey, command.ContentType, actualSize.Value, cancellationToken);
        }

        await storage.DeleteFileAsync(objectKey, cancellationToken);

        Uri url = await storage.GetPresignedUrlAsync(confirmedKey, ReadTtl, cancellationToken);

        return Result.Success(new LandingImageDto(
            command.ImageId,
            url.ToString(),
            dateTimeProvider.UtcNow.Add(ReadTtl)));
    }
}
