using Application.Abstractions.Tenancy;
using Application.Billing.Entitlements;

namespace Web.Api.Endpoints.Billing;

/// <summary>
/// sfc A3.1.5 (A1, A21, RN-011): one row per <see cref="PlanResource"/> with the dealer's current usage
/// against its plan limit. Read-only self-service info — any authenticated role may read it, no
/// permission beyond being logged into a dealer.
/// </summary>
internal sealed class GetUsage : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("billing/usage", async (
            ICurrentTenantService tenantService,
            IEntitlementService entitlementService,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<PlanUsageLine> usage = await entitlementService.GetUsageReportAsync(
                tenantService.DealerId, cancellationToken);

            return Results.Ok(usage);
        })
        .RequireAuthorization()
        .WithTags("Billing")
        .WithName("GetBillingUsage")
        .Produces<IReadOnlyList<PlanUsageLine>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
