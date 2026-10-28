using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Application.Users.Verification;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Users.Invitations.InviteUser;

/// <summary>
/// sfc A4.2 / A14 / RN-031: only <c>CanManageUsers</c> can invite. <c>RoleId</c> must belong to the caller's
/// dealer — the same 404 whether the role is foreign or does not exist (spr PR 0.2 / REV-SDD-03). A verified
/// user with the same email anywhere on the platform is a conflict, but an unverified/pending one is not
/// revealed. A single-use, 7-day token is emailed; a previous pending invitation for the same email in this
/// dealer is revoked.
/// sfc A3.1 (A1, A21, RN-011): the plan's user limit is checked before anything else, inside the same
/// advisory-lock-guarded transaction as the rest of this handler — see IEntitlementService.EnsureCanAddAsync.
/// </summary>
internal sealed class InviteUserCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    ICurrentTenantService tenant,
    IUserContext userContext,
    ITransactionalEmailSender emailSender,
    IConfiguration configuration,
    IDateTimeProvider clock,
    ILogger<InviteUserCommandHandler> logger,
    IEntitlementService entitlementService)
    : ICommandHandler<InviteUserCommand>
{
    public async Task<Result> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        Guid dealerId = tenant.DealerId;
        var email = new Email(command.Email);

        // sfc A3.1: the transaction only needs to span the entitlement check and the token INSERT — it
        // keeps the advisory lock held until that insert commits. Everything that follows (sending the
        // email, revoking the token on send failure) is already its own atomic SaveChangesAsync, exactly
        // as before this change, and stays outside this transaction.
        IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        User? inviter;
        DealerSettingsEntity? dealer;
        DateTime now;
        string secret;
        UserActionToken token;

        try
        {
            Result entitlementCheck = await entitlementService.EnsureCanAddAsync(dealerId, PlanResource.Users, cancellationToken);
            if (entitlementCheck.IsFailure)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return Result.Failure(entitlementCheck.Error);
            }

            bool roleBelongsToDealer = await context.Roles
                .AnyAsync(r => r.Id == command.RoleId && r.DealerId == dealerId, cancellationToken);
            if (!roleBelongsToDealer)
                return Result.Failure(RoleErrors.NotFound(command.RoleId));

            // Platform-wide check, but only a VERIFIED account is a conflict: an unverified/pending one must not
            // be revealed to the inviter (it could belong to someone mid self-registration elsewhere).
            bool verifiedAccountExists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => (string)u.Email == email.Value && u.EmailVerifiedAtUtc != null, cancellationToken);
            if (verifiedAccountExists)
                return Result.Failure(UserErrors.EmailNotUnique);

            inviter = await context.Users
                .FirstOrDefaultAsync(u => u.Id == userContext.UserId && u.DealerId == dealerId, cancellationToken);
            dealer = await context.DealerSettings
                .FirstOrDefaultAsync(d => d.DealerId == dealerId, cancellationToken);

            now = clock.UtcNow;

            // One pending invitation per (dealer, email): a re-invite revokes the previous one.
            List<UserActionToken> pending = await context.UserActionTokens
                .Where(t => t.Purpose == UserActionPurpose.Invitation && t.DealerId == dealerId
                    && t.Email == email.Value && t.ConsumedAtUtc == null)
                .ToListAsync(cancellationToken);

            secret = UserActionTokenHasher.NewToken();
            token = UserActionToken.Issue(
                pending, UserActionPurpose.Invitation, dealerId, email.Value,
                UserActionTokenHasher.Hash(secret), userId: null, roleId: command.RoleId, createdBy: userContext.UserId, nowUtc: now);
            context.UserActionTokens.Add(token);
            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
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

        string baseUrl = (configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
        try
        {
            await emailSender.SendAsync(
                EmailTemplate.Invitation,
                email.Value,
                new Dictionary<string, string>
                {
                    ["InviterName"] = inviter?.FirstName ?? string.Empty,
                    ["DealerName"] = dealer?.DealerName ?? string.Empty,
                    ["AcceptUrl"] = $"{baseUrl}/invitacion/aceptar?t={secret}",
                    ["ExpiresInDays"] = UserActionToken.LifetimeOf(UserActionPurpose.Invitation).TotalDays.ToString("0")
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not send the invitation email for dealer {DealerId}", dealerId);
            // The recipient never received the secret: do not leave a pending token nobody can use.
            token.Revoke(now);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure(InvitationErrors.EmailNotSent);
        }

        return Result.Success();
    }
}
