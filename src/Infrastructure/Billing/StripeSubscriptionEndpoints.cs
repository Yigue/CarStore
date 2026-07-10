using Application.Billing.Commands.CreateCheckoutSession;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

public static class StripeSubscriptionEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/checkout", async (
            CreateCheckoutSessionRequest request,
            ISender sender) =>
        {
            var command = new CreateCheckoutSessionCommand(request.DealerId, request.Email);
            var result = await sender.Send(command);

            return result.IsSuccess
                ? Results.Ok(new { url = result.Value.Url })
                : Results.BadRequest(result.Error);
        })
        .WithName("CreateCheckoutSession")
        .WithTags("Billing");

        builder.MapGet("subscriptions/status", async (
            HttpContext context,
            ISender sender) =>
        {
            var dealerIdClaim = context.User.FindFirst("dealer_id")?.Value;
            if (string.IsNullOrEmpty(dealerIdClaim) || !Guid.TryParse(dealerIdClaim, out var dealerId))
            {
                return Results.Unauthorized();
            }

            var query = new Application.Billing.Queries.GetSubscriptionStatus.GetSubscriptionStatusQuery(dealerId);
            var result = await sender.Send(query);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        })
        .WithName("GetSubscriptionStatus")
        .WithTags("Billing");

        builder.MapGet("subscriptions/checkout", (
            Microsoft.Extensions.Options.IOptions<StripeOptions> stripeOptions) =>
        {
            var isConfigured = !string.IsNullOrWhiteSpace(stripeOptions.Value.SecretKey);
            return Results.Ok(new { configured = isConfigured });
        })
        .WithName("CheckStripeConfiguration")
        .WithTags("Billing");

        return builder;
    }
}

public sealed record CreateCheckoutSessionRequest(Guid DealerId, string Email);
