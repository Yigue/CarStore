using System.Security.Cryptography;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Webhooks;
using SharedKernel;

namespace Application.Webhooks.Create;

internal sealed class CreateWebhookSubscriptionCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider
) : ICommandHandler<CreateWebhookSubscriptionCommand, CreateWebhookSubscriptionResponse>
{
    // 32 random bytes (64 hex chars) — comfortably above the domain's 16-char minimum
    // and matches common webhook-signing-secret sizing (e.g. Stripe, GitHub).
    private const int SecretLengthHexChars = 64;

    public async Task<Result<CreateWebhookSubscriptionResponse>> Handle(
        CreateWebhookSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        string secret = RandomNumberGenerator.GetHexString(SecretLengthHexChars);

        try
        {
            var subscription = WebhookSubscription.Create(
                tenantService.DealerId,
                command.Url,
                secret,
                command.EventTypes,
                dateTimeProvider.UtcNow);

            context.WebhookSubscriptions.Add(subscription);
            await context.SaveChangesAsync(cancellationToken);

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
            return Result.Failure<CreateWebhookSubscriptionResponse>(
                Error.Validation("Webhooks.DomainError", ex.Message));
        }
    }
}
