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

        return builder;
    }
}

public sealed record CreateCheckoutSessionRequest(Guid DealerId, string Email);
