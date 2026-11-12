using Application.Abstractions.Messaging;

namespace Application.Roles.Delete;

public sealed record DeleteRoleCommand(Guid RoleId) : ICommand;
