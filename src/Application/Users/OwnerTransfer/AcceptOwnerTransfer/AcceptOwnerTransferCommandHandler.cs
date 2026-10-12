using Application.Abstractions.Authentication;
using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Platform.AuditLogs;
using Application.Users.Verification;
using Domain.Platform;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel;
using DealerSettingsEntity = Domain.DealerSettings.DealerSettings;

namespace Application.Users.OwnerTransfer.AcceptOwnerTransfer;

/// <summary>
/// sfc A4.3 / A22 / RN-029 (design C17): second half of the double confirmation. The recipient, authenticated, presents the
/// token. Unknown, expired, used, foreign-dealer, wrong-purpose or wrong-recipient tokens all answer the same error. The
/// token is consumed atomically (single use); then the owner is swapped, the swap is audited and the provider billing
/// email follows (best effort). The former owner keeps its role and stays an active Admin.
/// </summary>
internal sealed class AcceptOwnerTransferCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenant,
    IUserContext userContext,
    ISubscriptionGateway gateway,
    PlatformAuditLogRecorder auditRecorder,
    IDateTimeProvider clock,
    ILogger<AcceptOwnerTransferCommandHandler> logger)
    : ICommandHandler<AcceptOwnerTransferCommand>
{
    public async Task<Result> Handle(AcceptOwnerTransferCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
            return Result.Failure(OwnerTransferErrors.InvalidToken);

        Guid dealerId = tenant.DealerId;
        Guid actorId = userContext.UserId;
        DateTime now = clock.UtcNow;
        string tokenHash = UserActionTokenHasher.Hash(command.Token);

        UserActionToken? token = await context.UserActionTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == UserActionPurpose.OwnerTransfer, cancellationToken);

        // Only the addressee, inside their own dealer, can use it: anything else is the same generic error and does not spend it.
        if (token is null || !token.IsPending(now) || token.DealerId != dealerId || token.UserId != actorId)
            return Result.Failure(OwnerTransferErrors.InvalidToken);

        DealerSettingsEntity? dealer = await context.DealerSettings
            .FirstOrDefaultAsync(d => d.DealerId == dealerId, cancellationToken);

        // The initiator must still be the owner (a newer transfer or an earlier swap invalidates this one).
        if (dealer?.OwnerUserId is null || token.CreatedBy != dealer.OwnerUserId)
            return Result.Failure(OwnerTransferErrors.InvalidToken);

        User? recipient = await context.Users
            .FirstOrDefaultAsync(u => u.Id == actorId && u.DealerId == dealerId, cancellationToken);
        if (recipient is null || !await OwnerTransferEligibility.IsEligibleAsync(context, recipient, dealerId, cancellationToken))
            return Result.Failure(OwnerTransferErrors.TargetNotEligible);

        // Atomic single use: of N concurrent accepts exactly one gets rowcount = 1.
        if (!await context.TryConsumeUserActionTokenAsync(token.Id, now, cancellationToken))
            return Result.Failure(OwnerTransferErrors.InvalidToken);

        Guid formerOwnerId = dealer.OwnerUserId.Value;
        string? formerEmail = (await context.Users
                .Where(u => u.Id == formerOwnerId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken))?.Value;

        dealer.SetOwner(recipient.Id);
        await auditRecorder.RecordAsync(
            dealer.Id,
            PlatformAuditAction.OwnerTransferred,
            recipient.Id,
            PlatformAuditActorKind.SelfService,
            now,
            sourceEventKey: $"owner-transfer:{token.Id}",
            reason: $"Titularidad transferida de {formerEmail ?? formerOwnerId.ToString()} a {recipient.Email.Value}",
            fallbackEmail: recipient.Email.Value,
            cancellationToken: cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Best effort: the swap is already committed, and a provider outage must not undo it.
        if (!string.IsNullOrWhiteSpace(dealer.StripeCustomerId))
        {
            try
            {
                await gateway.UpdateCustomerEmailAsync(dealer.StripeCustomerId, recipient.Email.Value, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Owner transferred for dealer {DealerId} but the provider email could not be updated", dealerId);
            }
        }

        return Result.Success();
    }
}
