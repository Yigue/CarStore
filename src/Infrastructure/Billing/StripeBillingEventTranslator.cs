using Application.Abstractions.Billing;
using System;
using System.Text.Json;

namespace Infrastructure.Billing;

/// <summary>
/// Stripe implementation of <see cref="IBillingEventTranslator"/> (sfc A2.1, ADR C14). Replaces the former
/// <c>StripeWebhookProcessor</c>: it only translates; the domain logic lives in <c>ApplyBillingEventCommand</c>.
/// Works on the raw JSON so partial payloads and API-version drift do not need the typed Stripe.net models.
/// </summary>
public sealed class StripeBillingEventTranslator : IBillingEventTranslator
{
    public BillingEvent? Translate(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("object", out var obj) ||
            obj.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Invalid event payload: missing data.object.", nameof(rawJson));
        }

        var eventId = GetString(root, "id") ?? string.Empty;
        var type = GetString(root, "type") ?? string.Empty;
        var occurredAt = root.TryGetProperty("created", out var created) && created.ValueKind == JsonValueKind.Number
            ? FromUnix(created.GetInt64())
            : DateTime.UtcNow;

        switch (type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                return new SubscriptionChanged(
                    eventId, occurredAt,
                    GetString(obj, "customer") ?? string.Empty,
                    GetString(obj, "id") ?? string.Empty,
                    GetString(obj, "status") ?? string.Empty,
                    GetUnix(obj, "current_period_start"),
                    GetUnix(obj, "current_period_end"),
                    GetUnix(obj, "trial_end"),
                    FirstPriceId(obj),
                    DealerIdFromMetadata(obj));

            case "customer.subscription.deleted":
                return new SubscriptionChanged(
                    eventId, occurredAt,
                    GetString(obj, "customer") ?? string.Empty,
                    GetString(obj, "id") ?? string.Empty,
                    "canceled",
                    null, null, null,
                    string.Empty,
                    DealerIdFromMetadata(obj));

            case "invoice.payment_succeeded":
                return new PaymentSucceeded(
                    eventId, occurredAt,
                    GetString(obj, "customer") ?? string.Empty,
                    GetString(obj, "subscription") ?? string.Empty,
                    GetUnix(obj, "period_start") ?? occurredAt,
                    GetUnix(obj, "period_end") ?? occurredAt);

            case "invoice.payment_failed":
                return new PaymentFailed(
                    eventId, occurredAt,
                    GetString(obj, "customer") ?? string.Empty,
                    GetString(obj, "subscription"),
                    obj.TryGetProperty("attempt_count", out var attempt) && attempt.ValueKind == JsonValueKind.Number
                        ? attempt.GetInt32()
                        : 1);

            case "customer.subscription.trial_will_end":
                return new TrialWillEnd(
                    eventId, occurredAt,
                    GetString(obj, "customer") ?? string.Empty,
                    GetString(obj, "id"),
                    GetUnix(obj, "trial_end"));

            case "charge.refunded":
                return new ChargeRefunded(
                    eventId, occurredAt,
                    GetString(obj, "customer") ?? string.Empty,
                    GetString(obj, "id") ?? string.Empty,
                    GetString(obj, "invoice"),
                    GetLong(obj, "amount"),
                    GetLong(obj, "amount_refunded"),
                    GetString(obj, "currency") ?? string.Empty,
                    FirstRefundReason(obj));

            case "checkout.session.completed":
                return new CheckoutCompleted(
                    eventId, occurredAt,
                    GetString(obj, "customer"),
                    GetString(obj, "subscription"),
                    DealerIdFromMetadata(obj));

            default:
                return null;
        }
    }

    private static DateTime FromUnix(long seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;

    private static long GetLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number ? prop.GetInt64() : 0;

    private static DateTime? GetUnix(JsonElement element, string name) =>
        element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? FromUnix(prop.GetInt64())
            : null;

    private static Guid? DealerIdFromMetadata(JsonElement obj) =>
        obj.TryGetProperty("metadata", out var metadata) &&
        metadata.ValueKind == JsonValueKind.Object &&
        Guid.TryParse(GetString(metadata, "dealer_id"), out var dealerId)
            ? dealerId
            : null;

    private static string FirstPriceId(JsonElement obj)
    {
        if (obj.TryGetProperty("items", out var items) &&
            items.TryGetProperty("data", out var itemsData) &&
            itemsData.ValueKind == JsonValueKind.Array &&
            itemsData.GetArrayLength() > 0 &&
            itemsData[0].TryGetProperty("price", out var price))
        {
            return GetString(price, "id") ?? string.Empty;
        }

        return string.Empty;
    }

    private static string? FirstRefundReason(JsonElement obj)
    {
        if (obj.TryGetProperty("refunds", out var refunds) &&
            refunds.TryGetProperty("data", out var refundsData) &&
            refundsData.ValueKind == JsonValueKind.Array &&
            refundsData.GetArrayLength() > 0)
        {
            return GetString(refundsData[0], "reason");
        }

        return null;
    }
}
