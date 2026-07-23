using Application.Abstractions.Messaging;
using Domain.Users;

namespace Application.Users.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Phone,
    Guid RoleId,
    bool IsActive
) : ICommand<Guid>;