using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.UpdateNotes;

internal sealed class UpdateLeadNotesCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateLeadNotesCommand>
{
    public async Task<Result> Handle(UpdateLeadNotesCommand command, CancellationToken cancellationToken)
    {
        var lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == command.LeadId, cancellationToken);

        if (lead is null)
            return Result.Failure(LeadErrors.NotFound(command.LeadId));

        lead.UpdateNotes(command.Notes);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
