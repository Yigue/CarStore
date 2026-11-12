using System.Net.Http.Json;
using System.Text.Json;
using Application.Abstractions.Integrations;
using Microsoft.Extensions.Logging;
using SharedKernel;

namespace Infrastructure.Integrations.WhatsApp;

/// <summary>
/// sfc C0.4 / RN-046: outbound side of the WhatsApp Business Cloud API connector. Drained by
/// <c>IntegrationCommandDispatcher</c> (provider <c>"whatsapp"</c>, operation <c>send_message</c>) through the
/// existing queue/rate-limiter — this class never calls Meta directly from a request thread.
/// <para>
/// Built against Meta's publicly documented Cloud API contract (message endpoint, template vs. free-form
/// messages, the 24h customer service window). All tests mock <see cref="IHttpClientFactory"/> — this has
/// never been exercised against a real WhatsApp Business Account; see the apply report for that caveat.
/// </para>
/// </summary>
public sealed class WhatsAppCloudApiClient(
    IWhatsAppCredentialResolver credentialResolver,
    IWhatsAppContactStateStore contactStateStore,
    IHttpClientFactory httpClientFactory,
    IDateTimeProvider dateTimeProvider,
    ILogger<WhatsAppCloudApiClient> logger) : IIntegrationClient
{
    public const string HttpClientName = "whatsapp";

    private const string GraphApiBaseUrl = "https://graph.facebook.com/v20.0";
    private static readonly TimeSpan CustomerServiceWindow = TimeSpan.FromHours(24);

    public string Provider => "whatsapp";

    public async Task<IntegrationResult> ExecuteAsync(IntegrationRequest request, CancellationToken cancellationToken)
    {
        WhatsAppOutboundPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WhatsAppOutboundPayload>(request.Payload);
        }
        catch (JsonException ex)
        {
            return IntegrationResult.Failure($"Invalid WhatsApp outbound payload: {ex.Message}");
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ToPhoneE164))
        {
            return IntegrationResult.Failure("WhatsApp outbound payload requires ToPhoneE164.");
        }

        // RN-046: an opted-out contact never receives another outbound message. This is a
        // deliberate no-op, not a provider failure — the command must not retry.
        if (await contactStateStore.IsOptedOutAsync(request.DealerId, payload.ToPhoneE164, cancellationToken))
        {
            logger.LogInformation(
                "Skipping WhatsApp send to a contact that opted out (dealer {DealerId}).", request.DealerId);
            return IntegrationResult.Success();
        }

        WhatsAppCredential? credential = await credentialResolver.ResolveAsync(request.DealerId, cancellationToken);
        if (credential is null)
        {
            return IntegrationResult.Failure("WhatsApp is not connected for this dealer.");
        }

        DateTime? lastInboundAtUtc = await contactStateStore.GetLastInboundAtUtcAsync(
            request.DealerId, payload.ToPhoneE164, cancellationToken);
        bool withinCustomerServiceWindow =
            lastInboundAtUtc.HasValue && dateTimeProvider.UtcNow - lastInboundAtUtc.Value < CustomerServiceWindow;

        object? body;
        if (withinCustomerServiceWindow && !string.IsNullOrWhiteSpace(payload.FreeFormText))
        {
            body = BuildFreeFormMessage(payload);
        }
        else if (!string.IsNullOrWhiteSpace(payload.TemplateName))
        {
            body = BuildTemplateMessage(payload);
        }
        else
        {
            // Meta's rule (RN-046): outside the 24h window only a template may be sent. A caller
            // that did not supply one cannot be served right now — fail rather than guess.
            return IntegrationResult.Failure(
                "Outside the 24h customer service window and no template was provided for this send.");
        }

        return await SendAsync(credential, body, cancellationToken);
    }

    private async Task<IntegrationResult> SendAsync(WhatsAppCredential credential, object body, CancellationToken cancellationToken)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);
            using var requestMessage = new HttpRequestMessage(
                HttpMethod.Post, $"{GraphApiBaseUrl}/{credential.PhoneNumberId}/messages")
            {
                Content = JsonContent.Create(body),
            };
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credential.AccessToken);

            HttpResponseMessage response = await client.SendAsync(requestMessage, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return IntegrationResult.Success();
            }

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return IntegrationResult.Failure($"WhatsApp Cloud API returned {(int)response.StatusCode}: {responseBody}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "WhatsApp Cloud API send failed");
            return IntegrationResult.Failure(ex.Message);
        }
    }

    private static object BuildFreeFormMessage(WhatsAppOutboundPayload payload) => new
    {
        messaging_product = "whatsapp",
        to = payload.ToPhoneE164,
        type = "text",
        text = new { body = payload.FreeFormText },
    };

    private static object BuildTemplateMessage(WhatsAppOutboundPayload payload) => new
    {
        messaging_product = "whatsapp",
        to = payload.ToPhoneE164,
        type = "template",
        template = new
        {
            name = payload.TemplateName,
            language = new { code = string.IsNullOrWhiteSpace(payload.TemplateLanguage) ? "es_AR" : payload.TemplateLanguage },
            components = payload.TemplateParams is { Count: > 0 }
                ? new[]
                {
                    new
                    {
                        type = "body",
                        parameters = payload.TemplateParams.Select(p => new { type = "text", text = p }).ToArray(),
                    },
                }
                : null,
        },
    };
}
