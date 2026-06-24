using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.Archive;

internal sealed class ArchiveLeadCommandHandler(IApplicationDbContext context)
    : ICommandHandler<ArchiveLeadCommand>
{
    public async Task<Result> Handle(ArchiveLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await context.Leads
            .IgnoreQueryFilters() // Include already archived leads to be idempotent
            .FirstOrDefaultAsync(l => l.Id == command.LeadId, cancellationToken);

        if (lead is null)
            return Result.Failure(LeadErrors.NotFound(command.LeadId));

        lead.Archive();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
