using Application.Abstractions.Messaging;

namespace Application.Clients.Restore;

public sealed record RestoreClientCommand(Guid Id) : ICommand<Guid>;
