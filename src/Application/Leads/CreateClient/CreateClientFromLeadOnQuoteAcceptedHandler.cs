using Application.Abstractions.Data;
using Domain.Quotes.Events;
using Domain.Quotes;
using Domain.Clients;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.CreateClient;

internal sealed class CreateClientFromLeadOnQuoteAcceptedHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) 
    : INotificationHandler<QuoteAcceptedDomainEvent>
{
    public async Task Handle(QuoteAcceptedDomainEvent notification, CancellationToken cancellationToken)
    {
        var quote = await context.Quotes
            .Include(q => q.Lead)
            .FirstOrDefaultAsync(q => q.Id == notification.QuoteId, cancellationToken);

        if (quote?.Lead is not null && quote.ClientId is null)
        {
            var lead = quote.Lead;

            // REQ-CRM-DEDUP-001 / ADR-4: check ConvertedClientId first (set when Negociación
            // already auto-created a Prospect Client for this lead); fall back to the legacy
            // email-match only when it is null.
            //
            // That fallback scopes by DealerId explicitly. This handler runs from the outbox, and
            // ProcessOutboxMessagesJob dispatches with no HTTP context, so HasTenant is false and
            // the global query filters are disabled for the whole of this method — the normal
            // state here, not an edge case. Unscoped, it matches clients of other dealerships,
            // and one buyer shopping at several agencies is ordinary.
            var existingClient = lead.ConvertedClientId is { } convertedClientId
                ? await context.Clients.FirstOrDefaultAsync(c => c.Id == convertedClientId, cancellationToken)
                : await context.Clients.FirstOrDefaultAsync(
                    c => c.Email == lead.Email && c.DealerId == lead.DealerId, cancellationToken);

            Client targetClient;

            if (existingClient is not null)
            {
                targetClient = existingClient;
            }
            else
            {
                // Create new client from lead
                var nameParts = lead.ClientName.Split(' ', 2);
                var firstName = nameParts[0];
                var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

                // Use a unique temporary DNI to avoid unique constraint violation.
                // Must fit the DNI column (varchar 20); the first 16 hex chars of the
                // lead id keep it unique. The record is completed with the real DNI later.
                var tempDni = $"TEMP{lead.Id:N}"[..20];

                targetClient = new Client(
                    lead.DealerId,
                    firstName,
                    lastName,
                    tempDni,
                    lead.Email.Value,
                    lead.Phone,
                    string.Empty,
                    dateTimeProvider.UtcNow,
                    Domain.Clients.Attributes.ClientType.Individual,
                    lead.Id);

                context.Clients.Add(targetClient);
            }

            // ADR-2: activate inline — the client reference is already loaded here,
            // deterministically (no race with the Negociación-stage creation handler).
            targetClient.Activate();

            // Link the new client to the lead
            lead.MarkConverted(targetClient.Id);

            // Assign the accepted quote and carry the rest of the lead's history
            // (other quotes + appointments) over to the new client.
            quote.AssignClient(targetClient.Id);

            var otherQuotes = await context.Quotes
                .Where(q => q.LeadId == lead.Id && q.ClientId == null && q.Id != quote.Id)
                .ToListAsync(cancellationToken);
            foreach (var other in otherQuotes)
                other.AssignClient(targetClient.Id);

            var appointments = await context.Appointments
                .Where(a => a.LeadId == lead.Id && a.ClientId == null)
                .ToListAsync(cancellationToken);
            foreach (var appointment in appointments)
                appointment.AssignClient(targetClient.Id);

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}