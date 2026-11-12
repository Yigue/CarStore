using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Billing.Entitlements;
using Application.Users.Verification;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Users.Invitations.AcceptInvitation;

/// <summary>
/// sfc A4.2 / A14 / RN-031: anonymous — unknown, expired, used or wrong-purpose tokens all answer the same
/// generic error and do not spend the token. On success the user is created ALREADY verified: accepting the
/// invitation email IS the verification. Looked up by SHA-256 hash with no tenant filter (there is no tenant
/// yet), consumed atomically (single use).
/// Security review (sfc A3.1): <c>InviteUserCommandHandler</c> also checks the plan's seat limit, but only
/// at invite time — the <see cref="User"/> row this handler creates is the actual resource the limit is
/// about, and nothing stops a dealer from sending N invitations (each individually under the limit, since
/// none of them has created a User yet) and then accepting more of them than seats allow. The authoritative,
/// TOCTOU-safe check is here, immediately before the token is consumed and the row inserted, inside the same
/// advisory-lock-guarded transaction.
/// </summary>
internal sealed class AcceptInvitationCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock,
    IEntitlementService entitlementService)
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

        // sfc A3.1: the transaction spans the entitlement check, the token consumption and the User
        // insert — the advisory lock must still be held when the insert commits, or a second concurrent
        // accept could pass the same check before the first one's row is visible.
        IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            Result entitlementCheck = await entitlementService.EnsureCanAddAsync(
                token.DealerId, PlanResource.Users, cancellationToken);
            if (entitlementCheck.IsFailure)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return Result.Failure<Guid>(entitlementCheck.Error);
            }

            // Atomic single use: of N concurrent accepts exactly one gets rowcount = 1.
            if (!await context.TryConsumeUserActionTokenAsync(token.Id, now, cancellationToken))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return Result.Failure<Guid>(InvitationErrors.InvalidToken);
            }

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

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Result.Success(user.Id);
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
