using Application.Abstractions.Messaging;
using Application.Cars.Queries.GetCarImages;

namespace Application.Cars.Commands.UploadCarImage;

/// <summary>
/// Server-mediated image upload (ADR-7). The endpoint reads the multipart file into a stream
/// and forwards it here. The handler uploads to MinIO and persists a <c>CarImage</c>.
/// </summary>
public sealed record UploadCarImageCommand(
    Guid CarId,
    Stream FileStream,
    string ContentType,
    string FileName,
    long SizeBytes) : ICommand<CarImageDto>;
