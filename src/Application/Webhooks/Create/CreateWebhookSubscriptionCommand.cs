using Application.Abstractions.Messaging;

namespace Application.Webhooks.Create;

public sealed record CreateWebhookSubscriptionCommand(
    string Url,
    IReadOnlyList<string> EventTypes
) : ICommand<CreateWebhookSubscriptionResponse>;

/// <summary>
/// The signing <see cref="Secret"/> is generated server-side and returned exactly once,
/// on creation — it is never exposed again by GetAll/GetById (mirrors API-key UX
/// elsewhere: show-once, then masked).
/// </summary>
public sealed record CreateWebhookSubscriptionResponse(
    Guid Id,
    string Url,
    string Secret,
    IReadOnlyList<string> EventTypes,
    bool IsActive,
    DateTime CreatedAtUtc);
