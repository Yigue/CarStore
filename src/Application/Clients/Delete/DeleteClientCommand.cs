using Application.Abstractions.Messaging;

namespace Application.Clients.Delete;

public sealed record DeleteClientCommand(Guid Id) : ICommand<Guid>;
