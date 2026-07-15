using Application.Abstractions.Data;
using Domain.Clients;
using Domain.Clients.Attributes;
using Domain.Leads;
using Domain.Leads.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.CreateClient;

/// <summary>
/// REQ-CRM-PROSPECT-001 / ADR-1 / ADR-3: auto-creates a <see cref="ClientStatus.Prospect"/>
/// Client when a Lead reaches <see cref="LeadStatus.Negociacion"/>, and marks that Client
/// <see cref="ClientStatus.Lost"/> when the same Lead later reaches
/// <see cref="LeadStatus.Perdido"/>. Mirrors
/// <c>Application.Appointments.EventHandlers.AdvanceLeadOnDemoAppointmentCreatedHandler</c>'s
/// pattern: subscribes to the domain event, runs after the originating command has already
/// committed (outbox-decoupled, see design.md §1), and is idempotent to tolerate outbox
/// retries.
/// </summary>
internal sealed class CreateClientFromLeadOnNegociacionHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider)
    : INotificationHandler<LeadStatusChangedDomainEvent>
{
    public async Task Handle(LeadStatusChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        if (notification.NewStatus is not (LeadStatus.Negociacion or LeadStatus.Perdido))
        {
            return;
        }

        var lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == notification.LeadId, cancellationToken);

        if (lead is null)
        {
            return;
        }

        if (notification.NewStatus == LeadStatus.Perdido)
        {
            if (lead.ConvertedClientId is { } lostClientId)
            {
                var lostClient = await context.Clients
                    .FirstOrDefaultAsync(c => c.Id == lostClientId, cancellationToken);

                lostClient?.MarkAsLost();
                await context.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        // Negociacion: find-or-create, ConvertedClientId first (ADR-4), then email fallback.
        Client? target = lead.ConvertedClientId is { } convertedClientId
            ? await context.Clients.FirstOrDefaultAsync(c => c.Id == convertedClientId, cancellationToken)
            : await context.Clients.FirstOrDefaultAsync(c => c.Email == lead.Email, cancellationToken);

        if (target is null)
        {
            var nameParts = lead.ClientName.Split(' ', 2);
            var firstName = nameParts[0];
            var lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty;

            // Unique temporary DNI to avoid the unique-constraint violation; mirrors the
            // existing convention in CreateClientFromLeadOnQuoteAcceptedHandler.
            var tempDni = $"TEMP{lead.Id:N}"[..20];

            target = new Client(
                lead.DealerId,
                firstName,
                lastName,
                tempDni,
                lead.Email.Value,
                lead.Phone,
                string.Empty,
                dateTimeProvider.UtcNow,
                ClientType.Individual,
                lead.Id);

            target.SetProspect();
            context.Clients.Add(target);
        }

        lead.MarkConverted(target.Id);

        await ReassignLeadArtifactsAsync(context, lead.Id, target.Id, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task ReassignLeadArtifactsAsync(
        IApplicationDbContext context,
        Guid leadId,
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var quotes = await context.Quotes
            .Where(q => q.LeadId == leadId && q.ClientId == null)
            .ToListAsync(cancellationToken);
        foreach (var quote in quotes)
            quote.AssignClient(clientId);

        var appointments = await context.Appointments
            .Where(a => a.LeadId == leadId && a.ClientId == null)
            .ToListAsync(cancellationToken);
        foreach (var appointment in appointments)
            appointment.AssignClient(clientId);
    }
}
