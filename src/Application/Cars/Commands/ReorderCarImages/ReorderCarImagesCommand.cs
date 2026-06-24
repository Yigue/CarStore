using Application.Abstractions.Messaging;

namespace Application.Cars.Commands.ReorderCarImages;

public sealed record ReorderCarImagesCommand(
    Guid CarId,
    IReadOnlyList<Guid> OrderedImageIds) : ICommand;
