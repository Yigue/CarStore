using Application.Abstractions.Data;
using Domain.Leads;
using Domain.Sales.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Leads.UpdateStatus;

/// <summary>
/// Mirrors <see cref="UpdateLeadStatusFromQuoteHandler"/>: when a sale is created for a
/// lead-originated deal, the lead auto-advances to <see cref="LeadStatus.Ganado"/>.
/// Fires even while the sale is still Pending — a created sale is a strong enough signal
/// that the lead converted (same timing the quote-acceptance flow already uses).
/// </summary>
internal sealed class UpdateLeadStatusFromSaleHandler(IApplicationDbContext context)
    : INotificationHandler<SaleCreatedDomainEvent>
{
    public async Task Handle(SaleCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        if (notification.LeadId is not { } leadId)
        {
            return;
        }

        Lead? lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);

        if (lead is null || lead.Status == LeadStatus.Ganado)
        {
            return;
        }

        // System-driven transition: bypasses sequential UI rules, same as the
        // quote-acceptance path. NOTE: sale cancellation does not revert this — the Lead
        // domain forbids moving backward once Ganado (see Lead.ForceStatus / UpdateStatus),
        // so there is no supported way to "un-win" a lead from here.
        lead.ForceStatus(LeadStatus.Ganado);

        await context.SaveChangesAsync(cancellationToken);
    }
}
