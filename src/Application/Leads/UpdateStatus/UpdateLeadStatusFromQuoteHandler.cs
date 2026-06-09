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

        if (quote?.Lead is not null && quote.Lead.Status != LeadStatus.Ganado)
        {
            quote.Lead.UpdateStatus(LeadStatus.Ganado);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}