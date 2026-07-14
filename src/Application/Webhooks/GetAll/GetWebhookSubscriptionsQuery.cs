using Application.Abstractions.Messaging;

namespace Application.Webhooks.GetAll;

public sealed record GetWebhookSubscriptionsQuery : IQuery<List<WebhookSubscriptionResponse>>;

/// <summary>Secret is masked here — full value is only ever returned once, at creation time.</summary>
public sealed record WebhookSubscriptionResponse(
    Guid Id,
    string Url,
    string MaskedSecret,
    IReadOnlyList<string> EventTypes,
    bool IsActive,
    DateTime CreatedAtUtc);
