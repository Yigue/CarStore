using Application.Abstractions.Messaging;

namespace Application.Cars.Delete;

public sealed record DeleteCarCommand(Guid CarId) : ICommand;
