using Application.Abstractions.Billing;
using Application.Abstractions.Data;
using Application.Abstractions.Tenancy;
using Domain.Billing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Web.Api.Infrastructure;

/// <summary>
/// sfc A3.2 (A2, A24, RN-020, RN-021): endpoint filter gating a route behind a plan FEATURE — mirrors
/// <c>.HasPermission(...)</c> (see <c>EndpointExtensions</c>) in shape, but checks the dealer's PLAN
/// (<see cref="IPlanCatalog.GetFeaturesAsync"/>, config-only — never calls the payment provider), not
/// its role permissions. Usage: <c>.RequireFeature("crm")</c> on a minimal-API route.
/// </summary>
public sealed class RequireFeatureFilter(string feature) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        IServiceProvider services = context.HttpContext.RequestServices;
        CancellationToken ct = context.HttpContext.RequestAborted;

        var tenantService = services.GetRequiredService<ICurrentTenantService>();
        var dbContext = services.GetRequiredService<IApplicationDbContext>();
        var planCatalog = services.GetRequiredService<IPlanCatalog>();

        DealerSubscription? subscription = await dbContext.DealerSubscriptions
            .FirstOrDefaultAsync(s => s.DealerId == tenantService.DealerId, ct);
        string planCode = subscription?.PlanCode ?? DealerSubscription.DefaultPlanCode;

        IReadOnlyList<string> features = await planCatalog.GetFeaturesAsync(planCode, ct);
        bool included = features.Any(f => string.Equals(f, feature, StringComparison.OrdinalIgnoreCase));

        if (!included)
        {
            return Results.Problem(
                title: "plan.feature_unavailable",
                detail: $"Tu plan actual no incluye la funcionalidad '{feature}'.",
                type: ProblemTypes.Forbidden,
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}

public static class RequireFeatureEndpointExtensions
{
    public static RouteHandlerBuilder RequireFeature(this RouteHandlerBuilder builder, string feature) =>
        builder.AddEndpointFilter(new RequireFeatureFilter(feature));
}
