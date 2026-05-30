using Application.Abstractions.Messaging;

namespace Application.Cars.Commands.SetCoverImage;

public sealed record SetCoverImageCommand(Guid CarId, Guid ImageId) : ICommand;
