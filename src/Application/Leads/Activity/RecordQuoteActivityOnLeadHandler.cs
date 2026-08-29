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
            // REQ-2.2: acceptance no longer closes the deal. The lead was already moved to
            // Negociación when the quote was raised (AdvanceLeadOnQuoteCreatedHandler), and it
            // stays there until a sale is registered — so the entry has to name the step the
            // agent still owes rather than announce a stage change that no longer happens.
            "Cotización aceptada — registrá la venta para cerrar el lead",
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
