using Application.Abstractions.Messaging;

namespace Application.Clients.SoftDelete;

public sealed record SoftDeleteClientCommand(Guid Id) : ICommand<Guid>;
