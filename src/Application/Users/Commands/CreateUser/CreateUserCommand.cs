using Application.Abstractions.Messaging;
using Domain.Users;

namespace Application.Users.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? Phone,
    UserRole Role
) : ICommand<Guid>;