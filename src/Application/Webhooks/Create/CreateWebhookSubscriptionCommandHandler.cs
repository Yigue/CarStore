using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;
using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SharedKernel;

namespace Application.Webhooks.Create;

internal sealed class CreateWebhookSubscriptionCommandHandler(
    IApplicationDbContext context,
    DbContext dbContext,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider,
    IEntitlementService entitlementService
) : ICommandHandler<CreateWebhookSubscriptionCommand, CreateWebhookSubscriptionResponse>
{
    // 32 random bytes (64 hex chars) — comfortably above the domain's 16-char minimum
    // and matches common webhook-signing-secret sizing (e.g. Stripe, GitHub).
    private const int SecretLengthHexChars = 64;

    public async Task<Result<CreateWebhookSubscriptionResponse>> Handle(
        CreateWebhookSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        // sfc A3.1 (A1, A21, RN-011): the transaction keeps the advisory-lock-guarded entitlement
        // check and the insert atomic — see ProvisionDealerCommandHandler and
        // IEntitlementService.EnsureCanAddAsync.
        IDbContextTransaction? transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            Result entitlementCheck = await entitlementService.EnsureCanAddAsync(
                tenantService.DealerId, PlanResource.Webhooks, cancellationToken);
            if (entitlementCheck.IsFailure)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return Result.Failure<CreateWebhookSubscriptionResponse>(entitlementCheck.Error);
            }

            string secret = RandomNumberGenerator.GetHexString(SecretLengthHexChars);

            var subscription = WebhookSubscription.Create(
                tenantService.DealerId,
                command.Url,
                secret,
                command.EventTypes,
                dateTimeProvider.UtcNow);

            context.WebhookSubscriptions.Add(subscription);
            await context.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return Result.Success(new CreateWebhookSubscriptionResponse(
                subscription.Id,
                subscription.Url,
                subscription.Secret,
                subscription.EventTypes,
                subscription.IsActive,
                subscription.CreatedAtUtc));
        }
        catch (DomainException ex)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return Result.Failure<CreateWebhookSubscriptionResponse>(
                Error.Validation("Webhooks.DomainError", ex.Message));
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
