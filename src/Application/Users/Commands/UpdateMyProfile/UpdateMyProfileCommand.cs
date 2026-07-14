using Application.Abstractions.Messaging;
using Application.Users.GetById;

namespace Application.Users.Commands.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    string FirstName,
    string LastName,
    string? Phone
) : ICommand<UserResponse>;
