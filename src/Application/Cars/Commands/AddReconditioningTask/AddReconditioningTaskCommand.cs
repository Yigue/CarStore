using Application.Abstractions.Messaging;

namespace Application.Cars.Commands.AddReconditioningTask;

public sealed record AddReconditioningTaskCommand(
    Guid CarId,
    string Description,
    decimal Cost,
    string Currency = "USD") : ICommand<Guid>;
