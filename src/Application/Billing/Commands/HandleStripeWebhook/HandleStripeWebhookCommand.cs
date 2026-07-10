using Application.Abstractions.Messaging;

namespace Application.Billing.Commands.HandleStripeWebhook;

public sealed record HandleStripeWebhookCommand(string EventId, string EventType, string RawJson) : ICommand;
