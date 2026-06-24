using Application.Abstractions.Messaging;

namespace Application.Users.Commands.GrantPermissions;

public sealed record GrantPermissionsCommand(
    Guid UserId,
    IEnumerable<string> Permissions
) : ICommand<GrantPermissionsResult>;

public sealed record GrantPermissionsResult(
    IEnumerable<string> Granted,
    IEnumerable<string> Revoked
);