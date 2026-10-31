using Application.Abstractions.Messaging;
using Application.Abstractions.Storage;
using Application.Abstractions.Tenancy;
using SharedKernel;

namespace Application.Dealers.LandingImages.GetLandingImageUploadUrl;

/// <summary>
/// A5.1: issues a presigned POST so the dealer's own browser can upload a landing-carousel
/// image straight to object storage, under a key namespaced by the CALLER's OWN tenant
/// (never a client-supplied dealer id). <see cref="Application.Dealers.LandingImages.ConfirmLandingImageUpload.ConfirmLandingImageUploadCommandHandler"/>
/// re-derives this exact same key from its own tenant, which is what makes cross-tenant
/// confirmation structurally impossible rather than merely checked.
/// </summary>
internal sealed class GetLandingImageUploadUrlCommandHandler(
    IStorageService storage,
    ICurrentTenantService tenant)
    : ICommandHandler<GetLandingImageUploadUrlCommand, LandingImageUploadUrlResponse>
{
    private static readonly TimeSpan UploadTtl = TimeSpan.FromMinutes(10);

    public async Task<Result<LandingImageUploadUrlResponse>> Handle(
        GetLandingImageUploadUrlCommand command,
        CancellationToken cancellationToken)
    {
        Guid imageId = Guid.NewGuid();
        string ext = Path.GetExtension(command.FileName).TrimStart('.');
        if (string.IsNullOrEmpty(ext)) ext = "jpg";

        string objectKey = $"landing/{tenant.DealerId}/{imageId}.{ext}";

        (string url, IReadOnlyDictionary<string, string> fields) =
            await storage.GeneratePresignedPostAsync(objectKey, command.ContentType, UploadTtl, cancellationToken);

        return new LandingImageUploadUrlResponse(imageId, url, fields);
    }
}
