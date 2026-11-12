using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Application.Common;
using Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Appointments.Commands.ConfirmAppointment;

/// <summary>
/// sfc B5.2.3 / H15: Requested to Scheduled. The agent must be an active user of the tenant
/// and the conflict is revalidated now that the agent is known.
/// </summary>
internal sealed class ConfirmAppointmentCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService) : ICommandHandler<ConfirmAppointmentCommand>
{
    public async Task<Result> Handle(ConfirmAppointmentCommand command, CancellationToken cancellationToken)
    {
        Result guard = TenantGuard.EnsureHasTenant(tenantService);
        if (guard.IsFailure)
            return guard;

        Guid dealerId = tenantService.DealerId;

        Appointment? appointment = await context.Appointments
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId && a.DealerId == dealerId, cancellationToken);

        if (appointment is null)
            return Result.Failure(AppointmentErrors.NotFound(command.AppointmentId));

        if (appointment.Status != AppointmentStatus.Requested)
            return Result.Failure(AppointmentErrors.InvalidStatusTransition(appointment.Status, AppointmentStatus.Scheduled));

        bool agentExists = await context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == command.AgentId && u.DealerId == dealerId && u.IsActive, cancellationToken);

        if (!agentExists)
            return Result.Failure(AppointmentErrors.AgentNotFound(command.AgentId));

        bool hasConflict = await context.Appointments.HasConflictAsync(
            dealerId,
            appointment.VehicleId,
            command.AgentId,
            appointment.StartDateTime,
            appointment.EndDateTime,
            excludeAppointmentId: appointment.Id,
            cancellationToken);

        if (hasConflict)
            return Result.Failure(AppointmentErrors.Conflict);

        try
        {
            appointment.Confirm(command.AgentId);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation("Appointment.DomainError", ex.Message));
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
