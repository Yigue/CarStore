using Application.Abstractions.Billing;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Billing;
using SharedKernel;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.Commands.CreateCheckoutSession;

internal sealed class CreateCheckoutSessionCommandHandler : ICommandHandler<CreateCheckoutSessionCommand, CheckoutSessionResponse>
{
    private readonly ISubscriptionGateway _subscriptionGateway;
    private readonly ICurrentTenantService _tenantService;

    public CreateCheckoutSessionCommandHandler(
        ISubscriptionGateway subscriptionGateway,
        ICurrentTenantService tenantService)
    {
        _subscriptionGateway = subscriptionGateway;
        _tenantService = tenantService;
    }

    public async Task<Result<CheckoutSessionResponse>> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantService.HasTenant || request.DealerId != _tenantService.DealerId)
        {
            return Result.Failure<CheckoutSessionResponse>(CheckoutErrors.DealerMismatch);
        }

        // spr PR 1.1 (ADR B1, D1): "plan único" — the caller never chooses a plan yet, there is exactly one.
        var checkoutUrl = await _subscriptionGateway.CreateCheckoutSessionAsync(
            request.DealerId, request.Email, DealerSubscription.DefaultPlanCode, CheckoutReturn.None, cancellationToken);
        return Result.Success(new CheckoutSessionResponse(checkoutUrl));
    }
}

internal static class CheckoutErrors
{
    public static readonly Error DealerMismatch = Error.Forbidden(
        "Billing.DealerMismatch",
        "El checkout solo puede crearse para el dealer autenticado.");
}
