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

        // Security review (sfc A4.2): a stale grant must not resurrect days later. The role, or the inviter's own
        // standing to grant it, may have changed since the email went out; re-check both before honoring the token.
        bool roleStillBelongsToDealer = await context.Roles
            .AnyAsync(r => r.Id == token.RoleId.Value && r.DealerId == token.DealerId, cancellationToken);
        if (!roleStillBelongsToDealer)
            return Result.Failure<Guid>(InvitationErrors.InvalidToken);

        if (token.CreatedBy is { } inviterId)
        {
            bool inviterCanStillManageUsers = await context.Users
                .Where(u => u.Id == inviterId && u.DealerId == token.DealerId && u.IsActive)
                .Join(context.RolePermissions, u => u.RoleId, rp => rp.RoleId, (u, rp) => rp.Permission)
                .AnyAsync(p => p == "CanManageUsers", cancellationToken);
            if (!inviterCanStillManageUsers)
                return Result.Failure<Guid>(InvitationErrors.InvalidToken);
        }

        // Security review (sfc A4.2): Email has a global unique index. Without this check a row claimed since the
        // invite went out — by any means, not only a race — would consume the token and then throw on SaveChanges,
        // locking the invitee out with no way to retry (the one-time secret would already be spent). A narrow
        // concurrent-request race between this check and the final SaveChanges below remains (same residual risk
        // accepted elsewhere in this codebase, e.g. ResendVerification's documented timing side channel).
        bool emailAlreadyClaimed = await context.Users.IgnoreQueryFilters()
            .AnyAsync(u => (string)u.Email == token.Email, cancellationToken);
        if (emailAlreadyClaimed)
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
