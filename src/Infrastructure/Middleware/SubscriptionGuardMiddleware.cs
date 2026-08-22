using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Application.Abstractions.Billing;
using Domain.Billing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Middleware;

public sealed class SubscriptionGuardMiddleware
{
    private readonly RequestDelegate _next;
    
    // Exempt routes: webhooks, health, subscription status, checkout, and public catalog routes.
    // Catalog routes might be matched by something else, let's look at requirements.
    // Task 3.1.2: POST /webhooks/stripe, GET /health, GET /api/v1/subscriptions/status, public catalog (anonymous, host-based). POST /api/v1/subscriptions/checkout
    private static readonly Regex[] ExemptPaths =
    {
        new Regex("^/webhooks/stripe$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("^/health$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("^/api/v1/subscriptions/status$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex("^/api/v1/subscriptions/checkout$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };

    private bool IsExemptCatalogRoute(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var isGet = context.Request.Method == HttpMethods.Get;
        var isAnonymous = context.User.Identity?.IsAuthenticated != true;
        
        // If it's a GET request to cars endpoints and the user is not authenticated, consider it public catalog.
        return isGet && isAnonymous && (path.StartsWith("/api/v1/cars", StringComparison.OrdinalIgnoreCase));
    }

    public SubscriptionGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Check path exemptions
        foreach (var regex in ExemptPaths)
        {
            if (regex.IsMatch(path))
            {
                await _next(context);
                return;
            }
        }

        if (IsExemptCatalogRoute(context))
        {
            await _next(context);
            return;
        }

        if (context.Items.TryGetValue("Tenant.DealerId", out var dealerIdObj) && dealerIdObj is System.Guid dealerId)
        {
            var cache = context.RequestServices.GetRequiredService<ISubscriptionStatusCache>();
            var status = await cache.GetAsync(dealerId, context.RequestAborted);
            
            if (status.HasValue && status.Value != SubscriptionStatus.Active && status.Value != SubscriptionStatus.Trialing)
            {
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    error = "subscription.suspended",
                    dealerId = dealerId,
                    reactivationUrl = (string?)null // Wait, where do we get this? Maybe just null in middleware, FE handles it by creating a checkout session?
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response), context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
