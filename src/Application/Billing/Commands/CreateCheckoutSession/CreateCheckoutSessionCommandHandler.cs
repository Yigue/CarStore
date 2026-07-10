using Application.Abstractions.Billing;
using Application.Abstractions.Messaging;
using SharedKernel;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.Commands.CreateCheckoutSession;

internal sealed class CreateCheckoutSessionCommandHandler : ICommandHandler<CreateCheckoutSessionCommand, CheckoutSessionResponse>
{
    private readonly ISubscriptionGateway _subscriptionGateway;

    public CreateCheckoutSessionCommandHandler(ISubscriptionGateway subscriptionGateway)
    {
        _subscriptionGateway = subscriptionGateway;
    }

    public async Task<Result<CheckoutSessionResponse>> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var checkoutUrl = await _subscriptionGateway.CreateCheckoutSessionAsync(request.DealerId, request.Email, cancellationToken);
        return Result.Success(new CheckoutSessionResponse(checkoutUrl));
    }
}
