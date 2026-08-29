namespace Application.Clients.GetActivity;

/// <summary>
/// Turns an outbox event class name into a sentence a person can read.
///
/// <para>
/// Deliberately keyed on the event name rather than parsed out of the stored JSON payload. The
/// payload shape is an internal serialization detail that changes whenever an event record gains
/// a field, and a timeline that breaks on a refactor is worse than one that stays plain.
/// </para>
/// </summary>
internal static class ClientActivityDescriptions
{
    private static readonly Dictionary<string, string> Sentences = new(StringComparer.Ordinal)
    {
        ["ClientCreatedDomainEvent"] = "Cliente creado",
        ["ClientSoftDeletedDomainEvent"] = "Cliente dado de baja",
        ["ClientRestoredDomainEvent"] = "Cliente restaurado",
        ["SaleCreatedDomainEvent"] = "Venta registrada",
        ["SaleCompletedDomainEvent"] = "Venta completada",
        ["SaleCancelledDomainEvent"] = "Venta cancelada",
        ["QuoteCreatedDomainEvent"] = "Cotización generada",
        ["QuoteAcceptedDomainEvent"] = "Cotización aceptada",
        ["QuoteRejectedDomainEvent"] = "Cotización rechazada",
    };

    /// <summary>
    /// Falls back to the event name with its suffix trimmed, so an event added on the server
    /// without a sentence here still reads as something rather than disappearing.
    /// </summary>
    public static string For(string eventType) =>
        Sentences.TryGetValue(eventType, out string? sentence)
            ? sentence
            : eventType.Replace("DomainEvent", string.Empty, StringComparison.Ordinal);
}
