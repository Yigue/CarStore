using Application.Abstractions.Tenancy;
using Application.Billing.Commands.CreatePortalSession;
using MediatR;
using Microsoft.Extensions.Options;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Billing;

/// <summary>
/// spr PR 1.3 (SPR-06/07/BE-BILLING-05, D2): Stripe-hosted "manage my billing" page — update card, see
/// invoices. Gated behind <c>FeatureFlags:BillingPortal</c>; this exact path is exempt from
/// <c>SubscriptionGuardMiddleware</c> so a dealer stuck in PastDue/Suspended can still reach it to fix
/// their payment method (the whole point of the feature, not an oversight).
/// </summary>
internal sealed class CreatePortalSession : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("subscriptions/portal-session", async (
            ISender sender,
            IOptions<FeatureFlagsOptions> featureFlags,
            ICurrentTenantService tenantService,
            CancellationToken cancellationToken) =>
        {
            if (!featureFlags.Value.BillingPortal)
            {
                return Results.NotFound();
            }

            var command = new CreatePortalSessionCommand(tenantService.DealerId);
            var result = await sender.Send(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission("CanManageSettings")
        .WithTags("Billing")
        .WithName("CreatePortalSession")
        .Produces<PortalSessionResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
