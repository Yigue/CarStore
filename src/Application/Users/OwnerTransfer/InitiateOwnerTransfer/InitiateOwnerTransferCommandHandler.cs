using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Application.Abstractions.Email;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Users.Verification;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Users.OwnerTransfer.InitiateOwnerTransfer;

/// <summary>
/// sfc A4.3 / A22 / RN-029 (design C17): first half of the double confirmation. Only the current owner can start it; the
/// target must belong to the same dealer (otherwise 404) and be an active, verified Admin. A single-use, hash-only,
/// 48 h token is emailed to the recipient. Nothing changes until the recipient accepts.
/// </summary>
internal sealed class InitiateOwnerTransferCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenant,
    IUserContext userContext,
    IPasswordHasher passwordHasher,
    ITransactionalEmailSender emailSender,
    IConfiguration configuration,
    IDateTimeProvider clock,
    ILogger<InitiateOwnerTransferCommandHandler> logger)
    : ICommandHandler<InitiateOwnerTransferCommand>
{
    public async Task<Result> Handle(InitiateOwnerTransferCommand command, CancellationToken cancellationToken)
    {
        Guid dealerId = tenant.DealerId;
        Guid actorId = userContext.UserId;

        DealerSettingsEntity? dealer = await context.DealerSettings
            .FirstOrDefaultAsync(d => d.DealerId == dealerId, cancellationToken);

        // Fails closed: a dealer without OwnerUserId has nobody who can start a transfer.
        if (dealer?.OwnerUserId is null || dealer.OwnerUserId != actorId)
            return Result.Failure(OwnerTransferErrors.NotOwner);

        // Re-authentication (same as the account deletion request): a stolen session alone cannot start a transfer.
        // Checked before the target lookup so a wrong password reveals nothing about the target.
        User? actor = await context.Users
            .FirstOrDefaultAsync(u => u.Id == actorId && u.DealerId == dealerId, cancellationToken);
        if (actor is null || !passwordHasher.Verify(command.Password ?? string.Empty, actor.PasswordHash))
            return Result.Failure(OwnerTransferErrors.ReauthenticationFailed);

        if (command.TargetUserId == actorId)
            return Result.Failure(OwnerTransferErrors.SameUser);

        // Tenant-scoped lookup: a user of another dealer is indistinguishable from an unknown one.
        User? target = await context.Users
            .FirstOrDefaultAsync(u => u.Id == command.TargetUserId && u.DealerId == dealerId, cancellationToken);
        if (target is null)
            return Result.Failure(OwnerTransferErrors.TargetNotFound);

        if (!await OwnerTransferEligibility.IsEligibleAsync(context, target, dealerId, cancellationToken))
            return Result.Failure(OwnerTransferErrors.TargetNotEligible);

        DateTime now = clock.UtcNow;

        // One pending transfer per dealer: a new one revokes the previous, even when it pointed to someone else.
        List<UserActionToken> pending = await context.UserActionTokens
            .Where(t => t.Purpose == UserActionPurpose.OwnerTransfer && t.DealerId == dealerId && t.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (UserActionToken previous in pending)
            previous.Revoke(now);

        string secret = UserActionTokenHasher.NewToken();
        UserActionToken token = UserActionToken.Issue(
            pending, UserActionPurpose.OwnerTransfer, dealerId, target.Email.Value,
            UserActionTokenHasher.Hash(secret), target.Id, null, actorId, now);
        context.UserActionTokens.Add(token);
        await context.SaveChangesAsync(cancellationToken);

        string baseUrl = (configuration["Frontend:BaseUrl"] ?? "https://carstore-app.com").TrimEnd('/');
        try
        {
            await emailSender.SendAsync(
                EmailTemplate.OwnerTransfer,
                target.Email.Value,
                new Dictionary<string, string>
                {
                    ["Name"] = target.FirstName,
                    ["DealerName"] = dealer.DealerName,
                    ["ConfirmUrl"] = $"{baseUrl}/aceptar-titularidad?token={secret}",
                    ["ExpiresInHours"] = UserActionToken.LifetimeOf(UserActionPurpose.OwnerTransfer).TotalHours.ToString("0")
                },
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not send the owner transfer email for dealer {DealerId}", dealerId);
            // The recipient never received the secret: do not leave a pending token nobody can use.
            token.Revoke(now);
            await context.SaveChangesAsync(cancellationToken);
            return Result.Failure(OwnerTransferErrors.EmailNotSent);
        }

        return Result.Success();
    }
}
