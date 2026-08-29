using Application.Abstractions.Data;
using Domain.Leads;
using Domain.Quotes.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Leads.UpdateStatus;

/// <summary>
/// Creating a quote for a lead advances it to <see cref="LeadStatus.Negociacion"/>.
///
/// <para>
/// The missing third of a pattern that already existed: a booked demo appointment advances the
/// lead (<c>AdvanceLeadOnDemoAppointmentCreatedHandler</c>) and a registered sale advances it
/// (<c>UpdateLeadStatusFromSaleHandler</c>), but nothing moved it on a quote — quotes only spoke
/// to the pipeline when <b>accepted</b>, jumping straight to Ganado. So Negociación was the one
/// stage a user had to set by hand, which is exactly why cancelling its dialog left a lead
/// negotiating with no number on the table.
/// </para>
///
/// <para>
/// Uses <see cref="Lead.ForceStatus"/> like its two siblings: this is a system-driven transition
/// that follows a fact, so it bypasses the sequential rules the UI enforces while still
/// respecting the no-regression invariant baked into ForceStatus.
/// </para>
/// </summary>
internal sealed class AdvanceLeadOnQuoteCreatedHandler(IApplicationDbContext context)
    : INotificationHandler<QuoteCreatedDomainEvent>
{
    public async Task Handle(QuoteCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        Lead? lead = await ResolveLeadAsync(notification.QuoteId, cancellationToken);

        if (lead is null)
        {
            return;
        }

        // Only pull a lead forward. A quote raised for a lead already at Negociación or beyond —
        // a second offer, or one attached after the sale — must not drag it back.
        if (lead.Status is not (LeadStatus.Nuevo or LeadStatus.Contactado or LeadStatus.Demostracion))
        {
            return;
        }

        lead.ForceStatus(LeadStatus.Negociacion);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Lead?> ResolveLeadAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        var quote = await context.Quotes
            .Where(q => q.Id == quoteId)
            .Select(q => new { q.LeadId, q.ClientId })
            .FirstOrDefaultAsync(cancellationToken);

        if (quote is null)
        {
            return null;
        }

        if (quote.LeadId is { } leadId)
        {
            return await context.Leads.FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);
        }

        // Quotes raised before enquiries created leads hang off the client instead.
        if (quote.ClientId is { } clientId)
        {
            Guid? originLeadId = await context.Clients
                .Where(c => c.Id == clientId)
                .Select(c => c.OriginLeadId)
                .FirstOrDefaultAsync(cancellationToken);

            if (originLeadId is { } origin)
            {
                return await context.Leads.FirstOrDefaultAsync(l => l.Id == origin, cancellationToken);
            }
        }

        return null;
    }
}
