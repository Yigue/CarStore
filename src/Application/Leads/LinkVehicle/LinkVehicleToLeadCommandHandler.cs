using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Cars;
using Domain.Leads;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Leads.LinkVehicle;

internal sealed class LinkVehicleToLeadCommandHandler(IApplicationDbContext context)
    : ICommandHandler<LinkVehicleToLeadCommand>
{
    public async Task<Result> Handle(LinkVehicleToLeadCommand command, CancellationToken cancellationToken)
    {
        var lead = await context.Leads
            .FirstOrDefaultAsync(l => l.Id == command.LeadId, cancellationToken);

        if (lead is null)
            return Result.Failure(LeadErrors.NotFound(command.LeadId));

        // Verify the car exists
        var carExists = await context.Cars
            .AnyAsync(c => c.Id == command.VehicleId, cancellationToken);

        if (!carExists)
            return Result.Failure(CarErrors.NotFound(command.VehicleId));

        try
        {
            lead.LinkVehicle(command.VehicleId);
        }
        catch (DomainException ex)
        {
            return Result.Failure(new Error("Lead.DomainError", ex.Message, ErrorType.Validation));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
