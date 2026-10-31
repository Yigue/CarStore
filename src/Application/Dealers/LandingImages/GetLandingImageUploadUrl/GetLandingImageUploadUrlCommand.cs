using Application.Abstractions.Messaging;

namespace Application.Dealers.LandingImages.GetLandingImageUploadUrl;

public sealed record GetLandingImageUploadUrlCommand(
    string FileName,
    string ContentType) : ICommand<LandingImageUploadUrlResponse>;

public sealed record LandingImageUploadUrlResponse(
    Guid ImageId,
    string Url,
    IReadOnlyDictionary<string, string> Fields);
