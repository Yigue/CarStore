using Application.Abstractions.Data;
using Domain.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Billing;

public static class StripeWebhookEndpoint
{
    public static IEndpointRouteBuilder MapStripeWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("webhooks/stripe", async (
            HttpContext context,
            [FromServices] IApplicationDbContext dbContext,
            [FromServices] IOptions<StripeOptions> options,
            [FromServices] ILogger<StripeWebhookEndpointLogger> logger) =>
        {
            var signatureHeader = context.Request.Headers["Stripe-Signature"].ToString();
            if (string.IsNullOrEmpty(signatureHeader))
            {
                logger.LogWarning("Missing Stripe-Signature header.");
                return Results.BadRequest("Missing Stripe-Signature header.");
            }

            context.Request.EnableBuffering();
            string json;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true))
            {
                json = await reader.ReadToEndAsync();
            }

            try
            {
                // Verify the webhook signature
                EventUtility.ConstructEvent(
                    json,
                    signatureHeader,
                    options.Value.WebhookSecret,
                    throwOnApiVersionMismatch: false
                );

                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = "StripeRaw",
                    Content = json,
                    OccurredOnUtc = DateTime.UtcNow
                };

                dbContext.OutboxMessages.Add(outboxMessage);
                await dbContext.SaveChangesAsync(context.RequestAborted);

                return Results.Ok();
            }
            catch (StripeException ex)
            {
                logger.LogWarning(ex, "Stripe signature verification failed.");
                return Results.BadRequest("Invalid signature.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing Stripe webhook.");
                return Results.Problem(ex.ToString());
            }
        })
        .AllowAnonymous()
        .WithName("StripeWebhook")
        .WithTags("Billing");

        return app;
    }
}

// Dummy class for logger typing
public sealed class StripeWebhookEndpointLogger { }
