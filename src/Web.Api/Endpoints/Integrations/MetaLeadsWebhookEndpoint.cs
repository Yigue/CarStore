using Application.Abstractions.Data;
using Application.Abstractions.Integrations;
using Infrastructure.Integrations.Meta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Web.Api.Endpoints.Integrations;

/// <summary>
/// sfc C1.4 / B13: Meta Leads Ads webhook. No sibling WhatsApp/MercadoLibre webhook endpoint existed under
/// <c>Web.Api/Endpoints/Integrations/**</c> yet at the time this was written, so this is the first entry in that
/// folder — later connectors (C1.2/C1.3) should mirror this file's shape (GET challenge + POST signed notification)
/// for consistency.
/// </summary>
internal sealed class MetaLeadsWebhookEndpoint : IEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // Meta's one-time subscription challenge, sent when a dealer connects their Page to lead-ads notifications.
        app.MapGet("integrations/meta/webhook", (
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromQuery(Name = "hub.challenge")] string? challenge,
            [FromServices] IConfiguration configuration) =>
        {
            string expectedToken = configuration[$"Integrations:{MetaLeadsWebhookHandler.Provider}:VerifyToken"] ?? string.Empty;

            if (!string.Equals(mode, "subscribe", StringComparison.Ordinal)
                || string.IsNullOrEmpty(expectedToken)
                || !string.Equals(verifyToken, expectedToken, StringComparison.Ordinal))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Text(challenge ?? string.Empty, "text/plain");
        })
        .AllowAnonymous()
        .WithTags("Integrations")
        .WithName("MetaLeadsWebhookVerify");

        // The actual leadgen notification: signed, carries only leadgen_id/page_id/form_id (never the answers).
        app.MapPost("integrations/meta/webhook", async (
            HttpContext context,
            [FromServices] IApplicationDbContext dbContext,
            [FromServices] ICredentialProtector credentialProtector,
            [FromServices] IDateTimeProvider clock,
            [FromServices] IConfiguration configuration,
            [FromServices] IHttpClientFactory httpClientFactory,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            ILogger logger = loggerFactory.CreateLogger("Infrastructure.Integrations.Meta.MetaLeadsWebhook");

            context.Request.EnableBuffering();
            byte[] bodyBytes;
            using (var buffer = new MemoryStream())
            {
                await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
                bodyBytes = buffer.ToArray();
                context.Request.Body.Position = 0;
            }

            string signatureHeader = context.Request.Headers["X-Hub-Signature-256"].ToString();
            string appSecret = configuration[$"Integrations:{MetaLeadsWebhookHandler.Provider}:AppSecret"] ?? string.Empty;

            if (!MetaSignatureVerifier.IsValid(bodyBytes, signatureHeader, appSecret))
            {
                logger.LogWarning("Meta Leads webhook: invalid or missing X-Hub-Signature-256.");
                return Results.Unauthorized();
            }

            MetaWebhookPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<MetaWebhookPayload>(bodyBytes, JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Meta Leads webhook: failed to parse payload.");
                return Results.BadRequest("Invalid payload.");
            }

            try
            {
                if (payload?.Entry is not null)
                {
                    var graphClient = new MetaGraphClient(httpClientFactory.CreateClient("meta-graph"));
                    var handler = new MetaLeadsWebhookHandler(dbContext, credentialProtector, graphClient, clock, logger);

                    foreach (MetaWebhookEntry entry in payload.Entry)
                    {
                        string pageId = entry.Id ?? string.Empty;
                        if (entry.Changes is null)
                        {
                            continue;
                        }

                        foreach (MetaWebhookChange change in entry.Changes)
                        {
                            if (!string.Equals(change.Field, "leadgen", StringComparison.Ordinal)
                                || string.IsNullOrWhiteSpace(change.Value?.LeadgenId))
                            {
                                continue;
                            }

                            await handler.HandleLeadgenChangeAsync(pageId, change.Value.LeadgenId, context.RequestAborted);
                        }
                    }
                }

                return Results.Ok();
            }
            catch (Exception ex)
            {
                // AllowAnonymous: never leak ex.ToString() to an unauthenticated caller (same rule as StripeWebhookEndpoint).
                logger.LogError(ex, "Error processing Meta Leads webhook.");
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
            }
        })
        .AllowAnonymous()
        .WithTags("Integrations")
        .WithName("MetaLeadsWebhook");
    }

    private sealed class MetaWebhookPayload
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("entry")]
        public List<MetaWebhookEntry>? Entry { get; set; }
    }

    private sealed class MetaWebhookEntry
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("changes")]
        public List<MetaWebhookChange>? Changes { get; set; }
    }

    private sealed class MetaWebhookChange
    {
        [JsonPropertyName("field")]
        public string? Field { get; set; }

        [JsonPropertyName("value")]
        public MetaWebhookLeadgenValue? Value { get; set; }
    }

    private sealed class MetaWebhookLeadgenValue
    {
        [JsonPropertyName("leadgen_id")]
        public string? LeadgenId { get; set; }

        [JsonPropertyName("page_id")]
        public string? PageId { get; set; }

        [JsonPropertyName("form_id")]
        public string? FormId { get; set; }
    }
}
