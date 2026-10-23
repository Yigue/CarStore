using Application.Billing.Queries.GetPublicPlans;
using MediatR;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Billing;

/// <summary>
/// spr PR 1.1 (SPR-02/BE-BILLING-07, ADR B1, D1): anonymous <c>GET /api/v1/billing/plans</c> — the landing
/// page's single source of truth for pricing ("landing = Stripe", D1). No auth: shown before signup.
/// </summary>
internal sealed class GetPublicPlans : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("billing/plans", async (
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetPublicPlansQuery(), cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags("Billing")
        .WithName("GetPublicPlans")
        .AllowAnonymous()
        .Produces<IReadOnlyList<PublicPlanResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
    }
}
