using Application.Abstractions.Messaging;

namespace Application.Cars.Commands.DeleteCarImage;

public sealed record DeleteCarImageCommand(Guid CarId, Guid ImageId) : ICommand;
