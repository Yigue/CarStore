using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Webhooks.GetAll;

internal sealed class GetWebhookSubscriptionsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetWebhookSubscriptionsQuery, List<WebhookSubscriptionResponse>>
{
    public async Task<Result<List<WebhookSubscriptionResponse>>> Handle(
        GetWebhookSubscriptionsQuery query,
        CancellationToken cancellationToken)
    {
        List<WebhookSubscription> subscriptions = await context.WebhookSubscriptions
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        List<WebhookSubscriptionResponse> results = subscriptions
            .Select(s => new WebhookSubscriptionResponse(
                s.Id,
                s.Url,
                Mask(s.Secret),
                s.EventTypes,
                s.IsActive,
                s.CreatedAtUtc))
            .ToList();

        return Result.Success(results);
    }

    private static string Mask(string secret) =>
        secret.Length <= 4 ? "****" : $"****{secret[^4..]}";
}
