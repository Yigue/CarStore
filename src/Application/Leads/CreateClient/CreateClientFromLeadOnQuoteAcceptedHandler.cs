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
            
            // Check if client with this email already exists
            var existingClient = await context.Clients
                .FirstOrDefaultAsync(c => c.Email.Value == lead.Email.Value, cancellationToken);

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

                targetClient = new Client(
                    lead.DealerId,
                    firstName,
                    lastName,
                    string.Empty, // DNI placeholder
                    lead.Email.Value,
                    lead.Phone,
                    string.Empty, // Address
                    dateTimeProvider.UtcNow);

                context.Clients.Add(targetClient);
            }

            // Assign the new client to the quote
            quote.AssignClient(targetClient.Id);
            
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}