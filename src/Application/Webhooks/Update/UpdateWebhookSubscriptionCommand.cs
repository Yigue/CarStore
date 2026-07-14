using Application.Abstractions.Messaging;

namespace Application.Webhooks.Update;

public sealed record UpdateWebhookSubscriptionCommand(
    Guid Id,
    string Url,
    IReadOnlyList<string> EventTypes,
    bool IsActive
) : ICommand;
