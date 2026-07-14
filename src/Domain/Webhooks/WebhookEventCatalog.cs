namespace Domain.Webhooks;

/// <summary>
/// v1 catalog of outgoing webhook event types. Each constant is the public,
/// tenant-facing event name (sent in the <c>X-CarStore-Event</c> header and used
/// when a dealer subscribes to specific event types).
/// <para>
/// Only domain events verified to exist at the time this catalog was written are
/// mapped here (see <see cref="FromDomainEventTypeName"/>). Do NOT add an entry
/// unless a matching <c>IDomainEvent</c> record exists in the Domain assembly.
/// </para>
/// </summary>
public static class WebhookEventCatalog
{
    public const string SaleCreated = "sale.created";
    public const string SaleCompleted = "sale.completed";
    public const string SaleCancelled = "sale.cancelled";
    public const string LeadStatusChanged = "lead.status_changed";
    public const string QuoteAccepted = "quote.accepted";
    public const string AppointmentCreated = "appointment.created";
    public const string ClientCreated = "client.created";

    public static readonly IReadOnlyList<string> All =
    [
        SaleCreated,
        SaleCompleted,
        SaleCancelled,
        LeadStatusChanged,
        QuoteAccepted,
        AppointmentCreated,
        ClientCreated,
    ];

    /// <summary>
    /// Maps the CLR type name of a domain event (as persisted in <c>OutboxMessage.Type</c>)
    /// to its catalog event name. Consumed by the outbox processor to decide which
    /// events fan out to tenant webhook subscriptions — see
    /// <c>Infrastructure.BackgroundJobs.ProcessOutboxMessagesJob</c>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> FromDomainEventTypeName =
        new Dictionary<string, string>
        {
            ["SaleCreatedDomainEvent"] = SaleCreated,
            ["SaleCompletedDomainEvent"] = SaleCompleted,
            ["SaleCancelledDomainEvent"] = SaleCancelled,
            ["LeadStatusChangedDomainEvent"] = LeadStatusChanged,
            ["QuoteAcceptedDomainEvent"] = QuoteAccepted,
            ["AppointmentCreatedDomainEvent"] = AppointmentCreated,
            ["ClientCreatedDomainEvent"] = ClientCreated,
        };

    public static bool IsValid(string eventType) => All.Contains(eventType);
}
