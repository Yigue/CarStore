using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Billing.Commands.CreatePortalSession;

internal sealed class CreatePortalSessionCommandHandler : ICommandHandler<CreatePortalSessionCommand, PortalSessionResponse>
{
    private readonly ISubscriptionGateway _subscriptionGateway;
    private readonly ICurrentTenantService _tenantService;
    private readonly IApplicationDbContext _context;

    public CreatePortalSessionCommandHandler(
        ISubscriptionGateway subscriptionGateway,
        ICurrentTenantService tenantService,
        IApplicationDbContext context)
    {
        _subscriptionGateway = subscriptionGateway;
        _tenantService = tenantService;
        _context = context;
    }

    public async Task<Result<PortalSessionResponse>> Handle(CreatePortalSessionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantService.HasTenant || request.DealerId != _tenantService.DealerId)
        {
            return Result.Failure<PortalSessionResponse>(PortalSessionErrors.DealerMismatch);
        }

        var subscription = await _context.DealerSubscriptions
            .FirstOrDefaultAsync(s => s.DealerId == request.DealerId, cancellationToken);

        if (subscription is null || string.IsNullOrWhiteSpace(subscription.StripeCustomerId))
        {
            return Result.Failure<PortalSessionResponse>(PortalSessionErrors.NoStripeCustomer);
        }

        var portalUrl = await _subscriptionGateway.CreatePortalSessionAsync(subscription.StripeCustomerId!, cancellationToken);
        return Result.Success(new PortalSessionResponse(portalUrl));
    }
}

internal static class PortalSessionErrors
{
    public static readonly Error DealerMismatch = Error.Forbidden(
        "Billing.DealerMismatch",
        "La sesión del portal solo puede crearse para el dealer autenticado.");

    public static readonly Error NoStripeCustomer = Error.Conflict(
        "Billing.NoStripeCustomer",
        "El dealer no tiene un customer de Stripe asociado todavía.");
}
