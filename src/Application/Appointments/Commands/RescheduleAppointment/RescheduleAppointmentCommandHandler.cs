using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Tenancy;
using Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Appointments.Commands.RescheduleAppointment;

/// <summary>
/// Reschedule a single appointment. The conflict query EXCLUDES the row being moved
/// (<c>a.Id != AppointmentId</c>) — otherwise it would always collide with itself.
/// </summary>
internal sealed class RescheduleAppointmentCommandHandler(
    IApplicationDbContext context,
    ICurrentTenantService tenantService,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<RescheduleAppointmentCommand>
{
    public async Task<Result> Handle(RescheduleAppointmentCommand command, CancellationToken cancellationToken)
    {
        Guid dealerId = tenantService.DealerId;

        Appointment? appointment = await context.Appointments
            .FirstOrDefaultAsync(a => a.Id == command.AppointmentId, cancellationToken);

        if (appointment is null)
            return Result.Failure(AppointmentErrors.NotFound(command.AppointmentId));

        bool hasConflict = await context.Appointments
            .AsNoTracking()
            .AnyAsync(a =>
                a.Id != command.AppointmentId &&
                a.DealerId == dealerId &&
                (a.VehicleId == appointment.VehicleId || a.AgentId == appointment.AgentId) &&
                a.StartDateTime < command.End &&
                a.EndDateTime > command.Start,
                cancellationToken);

        if (hasConflict)
            return Result.Failure(AppointmentErrors.Conflict);

        try
        {
            appointment.Reschedule(command.Start, command.End, dateTimeProvider.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Validation("Appointment.DomainError", ex.Message));
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
