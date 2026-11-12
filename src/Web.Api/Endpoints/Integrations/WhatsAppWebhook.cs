using System.Text;
using System.Text.Json;
using Application.Leads.RegisterWhatsAppInboundMessage;
using Infrastructure.Integrations.WhatsApp;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Integrations;

/// <summary>
/// sfc C0.4 / RN-046: inbound side of the WhatsApp Business Cloud API connector.
/// <para>
/// Verifies Meta's <c>X-Hub-Signature-256</c> (HMAC-SHA256 over the raw body, keyed with the
/// platform Meta App's secret — <c>WhatsApp:AppSecret</c>, the same app used for every connected
/// dealer's Embedded Signup, which is why this is one platform-level secret and not a per-dealer
/// one) before trusting anything in the payload. A valid message updates the contact's "last
/// inbound" timestamp (RN-046's 24h window), honors an opt-out keyword, and creates/appends the
/// Lead via <see cref="RegisterWhatsAppInboundMessageCommand"/>.
/// </para>
/// <para>
/// A recognized opt-out: WhatsApp does not have SMS's universal carrier-enforced "STOP" — Meta
/// expects the business itself to honor a contact's stated wish (the platform-level signal is a
/// user blocking/reporting the business, which this webhook cannot observe). This endpoint treats a
/// message whose trimmed text case-insensitively equals "stop", "baja" or "cancelar" as that signal.
/// </para>
/// </summary>
internal sealed class WhatsAppWebhook : IEndpoint
{
    private static readonly string[] OptOutKeywords = ["stop", "baja", "cancelar"];

    private static readonly JsonSerializerOptions WebhookJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("integrations/whatsapp/webhook", async (
            HttpContext httpContext,
            [FromServices] IConfiguration configuration,
            [FromServices] IWhatsAppCredentialResolver credentialResolver,
            [FromServices] IWhatsAppContactStateStore contactStateStore,
            [FromServices] ISender sender,
            [FromServices] IDateTimeProvider dateTimeProvider,
            [FromServices] ILogger<WhatsAppWebhookLogger> logger,
            CancellationToken cancellationToken) =>
        {
            string appSecret = configuration["WhatsApp:AppSecret"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(appSecret))
            {
                logger.LogWarning("WhatsApp webhook received but WhatsApp:AppSecret is not configured.");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            httpContext.Request.EnableBuffering();
            string rawBody;
            using (var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, false, 1024, leaveOpen: true))
            {
                rawBody = await reader.ReadToEndAsync(cancellationToken);
            }

            string? signatureHeader = httpContext.Request.Headers["X-Hub-Signature-256"];
            if (!WhatsAppWebhookSignatureVerifier.Verify(rawBody, signatureHeader, appSecret))
            {
                logger.LogWarning("WhatsApp webhook signature verification failed.");
                return Results.Unauthorized();
            }

            WhatsAppWebhookEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<WhatsAppWebhookEnvelope>(rawBody, WebhookJsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse WhatsApp webhook payload.");
                return Results.BadRequest("Invalid payload.");
            }

            try
            {
                await ProcessAsync(envelope, credentialResolver, contactStateStore, sender, dateTimeProvider, logger, cancellationToken);
            }
            catch (Exception ex)
            {
                // AllowAnonymous: never leak ex.ToString() to an unauthenticated caller. Meta will
                // retry a non-2xx, and everything above already happened idempotently per message.
                logger.LogError(ex, "Error processing WhatsApp webhook.");
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
            }

            // Meta expects a prompt 200 regardless of whether every message mapped to a known dealer.
            return Results.Ok();
        })
        .AllowAnonymous()
        .WithTags("integrations")
        .WithName("WhatsAppWebhook");
    }

    private static async Task ProcessAsync(
        WhatsAppWebhookEnvelope? envelope,
        IWhatsAppCredentialResolver credentialResolver,
        IWhatsAppContactStateStore contactStateStore,
        ISender sender,
        IDateTimeProvider dateTimeProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (envelope?.Entry is null)
        {
            return;
        }

        foreach (WhatsAppWebhookEntry entry in envelope.Entry)
        {
            foreach (WhatsAppWebhookChange change in entry.Changes ?? [])
            {
                WhatsAppWebhookValue? value = change.Value;
                string? phoneNumberId = value?.Metadata?.PhoneNumberId;
                if (value?.Messages is null || string.IsNullOrWhiteSpace(phoneNumberId))
                {
                    continue; // status callbacks, template updates, etc. — nothing to register
                }

                Guid? dealerId = await credentialResolver.ResolveDealerIdByPhoneNumberIdAsync(phoneNumberId, cancellationToken);
                if (dealerId is null)
                {
                    logger.LogWarning("WhatsApp webhook for an unknown phone_number_id {PhoneNumberId}.", phoneNumberId);
                    continue;
                }

                foreach (WhatsAppWebhookMessage message in value.Messages)
                {
                    if (string.IsNullOrWhiteSpace(message.From))
                    {
                        continue;
                    }

                    DateTime occurredAtUtc = ParseTimestamp(message.Timestamp, dateTimeProvider);
                    string text = message.Text?.Body ?? string.Empty;

                    await contactStateStore.SetLastInboundAtUtcAsync(dealerId.Value, message.From, occurredAtUtc, cancellationToken);

                    if (IsOptOutKeyword(text))
                    {
                        await contactStateStore.SetOptedOutAsync(dealerId.Value, message.From, true, cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        await sender.Send(
                            new RegisterWhatsAppInboundMessageCommand(dealerId.Value, message.From, text, occurredAtUtc),
                            cancellationToken);
                    }
                }
            }
        }
    }

    private static bool IsOptOutKeyword(string text) =>
        OptOutKeywords.Contains(text.Trim().ToLowerInvariant());

    private static DateTime ParseTimestamp(string? unixSeconds, IDateTimeProvider dateTimeProvider) =>
        long.TryParse(unixSeconds, out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : dateTimeProvider.UtcNow;
}

// Dummy class for logger typing — mirrors StripeWebhookEndpointLogger.
internal sealed class WhatsAppWebhookLogger;
