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

        // Check if client with same email already exists
        var existingClient = await context.Clients
            .FirstOrDefaultAsync(c => c.Email.Value == lead.Email.Value, cancellationToken);

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
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(targetClient.Id);
    }
}
