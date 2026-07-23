using Application.Abstractions.Messaging;
using Domain.Users;

namespace Application.Users.Commands.AssignRole;

public sealed record AssignRoleCommand(
    Guid UserId,
    Guid RoleId
) : ICommand<Guid>;