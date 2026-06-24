using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.AssignAgent;

internal sealed class AssignAgentToLeadCommandHandler(IApplicationDbContext context)
    : ICommandHandler<AssignAgentToLeadCommand>
{
    public async Task<Result> Handle(AssignAgentToLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == command.LeadId, cancellationToken);

        if (lead is null)
            return Result.Failure(LeadErrors.NotFound(command.LeadId));

        var agentExists = await context.Users
            .AnyAsync(u => u.Id == command.AgentId, cancellationToken);

        if (!agentExists)
            return Result.Failure(new Error(
                "Leads.AgentNotFound",
                $"User {command.AgentId} was not found.",
                ErrorType.NotFound));

        try
        {
            lead.AssignAgent(command.AgentId);
        }
        catch (DomainException ex)
        {
            return Result.Failure(new Error("Lead.DomainError", ex.Message, ErrorType.Validation));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
