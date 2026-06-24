using Application.Abstractions.Messaging;
using SharedKernel;

namespace Application.Cars.Commands.GetImageUploadUrl;

public sealed record GetImageUploadUrlCommand(
    Guid CarId,
    string FileName,
    string ContentType) : ICommand<ImageUploadUrlResponse>;

public sealed record ImageUploadUrlResponse(
    Guid ImageId,
    string Url,
    IReadOnlyDictionary<string, string> Fields);
