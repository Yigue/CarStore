using Application.Abstractions.Data;
using Domain.Quotes.Events;
using Domain.Leads;
using Domain.Quotes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Leads.UpdateStatus;

internal sealed class UpdateLeadStatusFromQuoteHandler(IApplicationDbContext context)
    : INotificationHandler<QuoteAcceptedDomainEvent>
{
    public async Task Handle(QuoteAcceptedDomainEvent notification, CancellationToken cancellationToken)
    {
        var quote = await context.Quotes
            .Include(q => q.Lead)
            .FirstOrDefaultAsync(q => q.Id == notification.QuoteId, cancellationToken);

        Lead? leadToUpdate = quote?.Lead;

        if (leadToUpdate is null && quote?.ClientId is not null)
        {
            var client = await context.Clients
                .FirstOrDefaultAsync(c => c.Id == quote.ClientId, cancellationToken);
            if (client?.OriginLeadId is not null)
            {
                leadToUpdate = await context.Leads
                    .FirstOrDefaultAsync(l => l.Id == client.OriginLeadId.Value, cancellationToken);
            }
        }

        if (leadToUpdate is not null && leadToUpdate.Status != LeadStatus.Ganado)
        {
            // System-driven transition: quote acceptance auto-advances the lead to Ganado
            // regardless of current stage (bypasses sequential UI rules).
            leadToUpdate.ForceStatus(LeadStatus.Ganado);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
