using Application.Abstractions.Messaging;

namespace Application.Cars.Commands.CompleteReconditioningTask;

public sealed record CompleteReconditioningTaskCommand(
    Guid CarId,
    Guid TaskId) : ICommand;
