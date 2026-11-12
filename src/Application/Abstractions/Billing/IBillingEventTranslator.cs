namespace Application.Abstractions.Billing;

/// <summary>
/// Per-provider translator from a raw webhook payload to a normalized <see cref="BillingEvent"/>
/// (sfc ADR C14). Stripe is v1; a second provider adds a second implementation.
/// </summary>
public interface IBillingEventTranslator
{
    /// <summary>
    /// Returns the normalized event, or null when the provider event type is not one the system acts on.
    /// Throws <see cref="System.ArgumentException"/> when the payload is malformed.
    /// </summary>
    BillingEvent? Translate(string rawJson);
}
