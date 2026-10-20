using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Users.Verification;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Users.Invitations.AcceptInvitation;

/// <summary>
/// sfc A4.2 / A14 / RN-031: anonymous — unknown, expired, used or wrong-purpose tokens all answer the same
/// generic error and do not spend the token. On success the user is created ALREADY verified: accepting the
/// invitation email IS the verification. Looked up by SHA-256 hash with no tenant filter (there is no tenant
/// yet), consumed atomically (single use).
/// </summary>
internal sealed class AcceptInvitationCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher, IDateTimeProvider clock)
    : ICommandHandler<AcceptInvitationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
            return Result.Failure<Guid>(InvitationErrors.InvalidToken);

        DateTime now = clock.UtcNow;
        string tokenHash = UserActionTokenHasher.Hash(command.Token);

        UserActionToken? token = await context.UserActionTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == UserActionPurpose.Invitation, cancellationToken);

        if (token is null || token.RoleId is null || !token.IsPending(now))
            return Result.Failure<Guid>(InvitationErrors.InvalidToken);

        // Atomic single use: of N concurrent accepts exactly one gets rowcount = 1.
        if (!await context.TryConsumeUserActionTokenAsync(token.Id, now, cancellationToken))
            return Result.Failure<Guid>(InvitationErrors.InvalidToken);

        var user = new User(
            token.DealerId,
            token.Email,
            command.FirstName.Trim(),
            command.LastName.Trim(),
            passwordHasher.Hash(command.Password),
            token.RoleId.Value);

        // Accepting the emailed invitation already proves control of the address.
        user.MarkEmailVerified(now);

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(user.Id);
    }
}
