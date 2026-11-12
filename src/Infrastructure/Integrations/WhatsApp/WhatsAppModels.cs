namespace Infrastructure.Integrations.WhatsApp;

/// <summary>
/// sfc C0.4 / RN-046: what a dealer's "Connect WhatsApp" (Embedded Signup) flow hands over, stored as JSON inside
/// <see cref="Domain.Integrations.DealerIntegration.EncryptedCredentials"/> (protected via <c>ICredentialProtector</c>,
/// purpose <c>integrations.credentials.v1</c> — same as every other provider; the entity never sees the plaintext).
/// </summary>
public sealed record WhatsAppCredential(
    string AccessToken,
    string PhoneNumberId,
    string? WabaId = null);

/// <summary>
/// What <see cref="WhatsAppCloudApiClient"/> receives as the payload of an outbound
/// <c>Domain.Integrations.IntegrationCommand</c> (operation <c>send_message</c>).
/// <para>
/// Meta's Cloud API rule (RN-046): a free-form message (<see cref="FreeFormText"/>) is only
/// deliverable within 24h of the contact's last inbound message (the "customer service window").
/// Outside that window only a pre-approved template (<see cref="TemplateName"/>) can be sent.
/// The caller may supply both; the client picks based on the window and fails loudly if neither
/// applies.
/// </para>
/// </summary>
public sealed record WhatsAppOutboundPayload(
    string ToPhoneE164,
    string? FreeFormText,
    string? TemplateName,
    string? TemplateLanguage,
    IReadOnlyList<string>? TemplateParams);

/// <summary>Minimal subset of Meta's inbound webhook payload (Cloud API, <c>field: "messages"</c>) that this connector reads.</summary>
public sealed record WhatsAppWebhookEnvelope(string? Object, List<WhatsAppWebhookEntry>? Entry);

public sealed record WhatsAppWebhookEntry(string? Id, List<WhatsAppWebhookChange>? Changes);

public sealed record WhatsAppWebhookChange(WhatsAppWebhookValue? Value, string? Field);

public sealed record WhatsAppWebhookValue(
    WhatsAppWebhookMetadata? Metadata,
    List<WhatsAppWebhookMessage>? Messages);

public sealed record WhatsAppWebhookMetadata(string? PhoneNumberId, string? DisplayPhoneNumber);

public sealed record WhatsAppWebhookMessage(string? From, string? Id, string? Timestamp, string? Type, WhatsAppWebhookText? Text);

public sealed record WhatsAppWebhookText(string? Body);
