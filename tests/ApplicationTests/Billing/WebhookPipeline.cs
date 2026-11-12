using Application.Abstractions.Billing;
using Application.Billing.ApplyBillingEvent;
using Application.Billing.Commands.HandleStripeWebhook;
using Infrastructure.Billing;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SharedKernel;

namespace ApplicationTests.Billing;

/// <summary>Wires HandleStripeWebhook -> Stripe translator -> ApplyBillingEvent without a DI container.</summary>
internal static class WebhookPipeline
{
    public static HandleStripeWebhookCommandHandler Create(IDealerSubscriptionRepository repository)
    {
        var apply = new ApplyBillingEventCommandHandler(repository, new Mock<IPublisher>().Object, NullLogger<ApplyBillingEventCommandHandler>.Instance);
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<IRequest<Result>>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<Result> request, CancellationToken ct) =>
                apply.Handle((ApplyBillingEventCommand)request, ct));
        return new HandleStripeWebhookCommandHandler(new StripeBillingEventTranslator(), sender.Object);
    }
}
