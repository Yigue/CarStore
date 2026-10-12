using Application.Abstractions.Billing;
using Application.Abstractions.Messaging;
using Application.Billing.ApplyBillingEvent;
using MediatR;
using SharedKernel;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.Commands.HandleStripeWebhook;

/// <summary>
/// Entry point of the outbox job: translates the raw provider payload into a normalized
/// <see cref="BillingEvent"/> and delegates the domain logic to <see cref="ApplyBillingEventCommand"/>.
/// </summary>
internal sealed class HandleStripeWebhookCommandHandler : ICommandHandler<HandleStripeWebhookCommand>
{
    private readonly IBillingEventTranslator _translator;
    private readonly ISender _sender;

    public HandleStripeWebhookCommandHandler(IBillingEventTranslator translator, ISender sender)
    {
        _translator = translator;
        _sender = sender;
    }

    public async Task<Result> Handle(HandleStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        BillingEvent? billingEvent;
        try
        {
            billingEvent = _translator.Translate(request.RawJson);
        }
        catch (Exception ex) when (ex is ArgumentException or System.Text.Json.JsonException)
        {
            return Result.Failure(Error.Validation("Webhook.InvalidPayload", "Invalid event payload."));
        }

        if (billingEvent is null)
        {
            return Result.Success();
        }

        return await _sender.Send(new ApplyBillingEventCommand(billingEvent), cancellationToken);
    }
}
