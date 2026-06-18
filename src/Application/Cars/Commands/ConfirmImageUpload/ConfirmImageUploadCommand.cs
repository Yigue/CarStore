using Application.Abstractions.Messaging;
using Application.Cars.Queries.GetCarImages;
using SharedKernel;

namespace Application.Cars.Commands.ConfirmImageUpload;

public sealed record ConfirmImageUploadCommand(
    Guid CarId,
    Guid ImageId,
    string FileName,
    string ContentType,
    long SizeBytes) : ICommand<CarImageDto>;
