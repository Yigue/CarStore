using Application.Abstractions.Messaging;

namespace Application.Webhooks.Delete;

public sealed record DeleteWebhookSubscriptionCommand(Guid Id) : ICommand;
