using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Users.Verification;
using Domain.Shared.ValueObjects;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
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
/// </summary>
internal sealed class InviteUserCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenant,
    IUserContext userContext,
    ITransactionalEmailSender emailSender,
    IConfiguration configuration,
    IDateTimeProvider clock,
    ILogger<InviteUserCommandHandler> logger)
    : ICommandHandler<InviteUserCommand>
{
    public async Task<Result> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        Guid dealerId = tenant.DealerId;
        var email = new Email(command.Email);

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

        User? inviter = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userContext.UserId && u.DealerId == dealerId, cancellationToken);
        DealerSettingsEntity? dealer = await context.DealerSettings
            .FirstOrDefaultAsync(d => d.DealerId == dealerId, cancellationToken);

        DateTime now = clock.UtcNow;

        // One pending invitation per (dealer, email): a re-invite revokes the previous one.
        List<UserActionToken> pending = await context.UserActionTokens
            .Where(t => t.Purpose == UserActionPurpose.Invitation && t.DealerId == dealerId
                && t.Email == email.Value && t.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);

        string secret = UserActionTokenHasher.NewToken();
        UserActionToken token = UserActionToken.Issue(
            pending, UserActionPurpose.Invitation, dealerId, email.Value,
            UserActionTokenHasher.Hash(secret), userId: null, roleId: command.RoleId, createdBy: userContext.UserId, nowUtc: now);
        context.UserActionTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);

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
