using Application.Abstractions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Clients;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.Convert;

internal sealed class ConvertLeadToClientCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<ConvertLeadToClientCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ConvertLeadToClientCommand command, CancellationToken cancellationToken)
    {
        var lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == command.LeadId, cancellationToken);

        if (lead is null)
            return Result.Failure<Guid>(LeadErrors.NotFound(command.LeadId));

        // Check if client with same email already exists.
        // Compare the Email value object directly (not .Value) so EF translates it
        // through the value converter — `c.Email.Value == ...` is not translatable
        // against the relational provider and throws at runtime.
        var existingClient = await context.Clients
            .FirstOrDefaultAsync(c => c.Email == lead.Email, cancellationToken);

        Client targetClient;

        if (existingClient is not null)
        {
            targetClient = existingClient;
        }
        else
        {
            var nameParts = lead.ClientName.Split(' ', 2);
            var firstName = nameParts[0];
            var lastName = nameParts.Length > 1 ? nameParts[1] : firstName;

            targetClient = new Client(
                lead.DealerId,
                firstName,
                lastName,
                command.Dni,
                lead.Email.Value,
                lead.Phone,
                command.Address,
                dateTimeProvider.UtcNow,
                Domain.Clients.Attributes.ClientType.Individual,
                lead.Id);

            context.Clients.Add(targetClient);
        }

        lead.MarkConverted(targetClient.Id);

        if (lead.Status != LeadStatus.Ganado)
            lead.ForceStatus(LeadStatus.Ganado);

        // Carry the lead's history over to the client so the relationship is preserved:
        // every quote/appointment created while it was a lead now belongs to the client.
        await ReassignLeadArtifactsAsync(context, lead.Id, targetClient.Id, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(targetClient.Id);
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
