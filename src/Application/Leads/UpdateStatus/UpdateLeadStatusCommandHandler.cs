using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.UpdateStatus;

internal sealed class UpdateLeadStatusCommandHandler(
    IApplicationDbContext context
) : ICommandHandler<UpdateLeadStatusCommand>
{
    public async Task<Result> Handle(UpdateLeadStatusCommand command, CancellationToken cancellationToken)
    {
        var lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == command.LeadId, cancellationToken);

        if (lead is null)
            return Result.Failure(LeadErrors.NotFound(command.LeadId));

        try
        {
            lead.UpdateStatus(command.NewStatus);
        }
        catch (DomainException ex)
        {
            return Result.Failure(new Error("Lead.DomainError", ex.Message, ErrorType.Validation));
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
