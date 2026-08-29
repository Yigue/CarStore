using Application.Abstractions.Data;
using Domain.Leads;
using Domain.Quotes.Events;
using MediatR;
using SharedKernel;

namespace Application.Leads.Activity;

/// <summary>
/// Folds a quote's life into the lead's history.
///
/// <para>
/// <see cref="QuoteRejectedDomainEvent"/> is the reason this exists. It was raised by
/// <c>Quote.Reject</c> and had <b>no subscriber at all</b> — its twin, QuoteAccepted, had two — so
/// a rejection and its reason vanished the moment it happened. An agent reopening the lead saw a
/// quote that had simply stopped mattering, with nothing saying why.
/// </para>
/// </summary>
internal sealed class RecordQuoteActivityOnLeadHandler(
    IApplicationDbContext context,
    LeadActivityRecorder recorder,
    IDateTimeProvider dateTimeProvider)
    : INotificationHandler<QuoteCreatedDomainEvent>,
      INotificationHandler<QuoteAcceptedDomainEvent>,
      INotificationHandler<QuoteRejectedDomainEvent>
{
    public Task Handle(QuoteCreatedDomainEvent notification, CancellationToken cancellationToken) =>
        RecordAsync(
            notification.QuoteId,
            LeadActivityType.QuoteCreated,
            $"Cotización generada por {notification.ProposedPrice.Amount:N0} {notification.ProposedPrice.Currency}",
            cancellationToken);

    public Task Handle(QuoteAcceptedDomainEvent notification, CancellationToken cancellationToken) =>
        RecordAsync(
            notification.QuoteId,
            LeadActivityType.QuoteAccepted,
            // Accepting a quote force-advances the lead to Ganado (UpdateLeadStatusFromQuoteHandler),
            // skipping the sequential rules the board enforces. Saying so here is what turns that
            // jump from something that looks like a bug into something the agent can follow.
            "Cotización aceptada — el lead pasa a Ganado automáticamente",
            cancellationToken);

    public Task Handle(QuoteRejectedDomainEvent notification, CancellationToken cancellationToken) =>
        RecordAsync(
            notification.QuoteId,
            LeadActivityType.QuoteRejected,
            string.IsNullOrWhiteSpace(notification.Reason)
                ? "Cotización rechazada"
                : $"Cotización rechazada — {notification.Reason}",
            cancellationToken);

    private async Task RecordAsync(
        Guid quoteId,
        LeadActivityType type,
        string description,
        CancellationToken cancellationToken)
    {
        Lead? lead = await recorder.FindLeadForQuoteAsync(quoteId, cancellationToken);

        if (lead is null)
        {
            return;
        }

        bool recorded = await recorder.RecordAsync(
            lead,
            type,
            description,
            dateTimeProvider.UtcNow,
            cancellationToken,
            relatedEntityId: quoteId,
            relatedEntityType: "Quote");

        if (recorded)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
